using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostAIBase : NetworkBehaviour
{
    public GhostType ghostType;

    public int attackDamage = 1;
    public float speedMove = 4f;

    public AudioSource screamAudioSource;
    public AudioClip screamClip;

    protected NavMeshAgent agent;
    protected Animator animator;

    public Transform[] roamPoints;
    public float roamRadius = 15f;
    public float roamWaitMin = 2f;
    public float roamWaitMax = 5f;

    public float appearInterval = 25f;
    public float screamInterval = 20f;
    public float teleportInterval = 30f;
    public float disturbInterval = 15f;
    public float lightInterval = 12f;
    public float doorInterval = 18f;
    public float attackInterval = 5f;

    private float _appearTimer;
    private float _screamTimer;
    private float _teleportTimer;
    private float _disturbTimer;
    private float _lightTimer;
    private float _doorTimer;
    private float _attackTimer;

    private readonly List<Vector3> _learnedPositions = new();
    private float _learnTimer = 5f;

    protected enum AIState { Roaming, Disturbing, Hunting }
    protected AIState state = AIState.Roaming;

    private const float StateCheckInterval = 0.3f; 
    private float _stateCheckTimer;
    private Coroutine _roamCoroutine;

    public float sightRange = 12f;
    public float attackRange = 2f;

    protected PlayerMovement currentTarget;

    public float interactRadius = 3f;

    private Plate _targetPlate;

    public LayerMask waterLayer;
    private Transform _cachedSpawn;

    protected int Phase => GamePhaseManager.Instance?.currentPhase.Value ?? 1;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.speed = speedMove;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.autoTraverseOffMeshLink = true;

        agent.updateUpAxis = true;

        _appearTimer = Random.Range(appearInterval * 0.3f, appearInterval);
        _screamTimer = Random.Range(screamInterval * 0.3f, screamInterval);
        _teleportTimer = Random.Range(teleportInterval * 0.3f, teleportInterval);
        _disturbTimer = Random.Range(disturbInterval * 0.5f, disturbInterval);
        _lightTimer = Random.Range(lightInterval * 0.5f, lightInterval);
        _doorTimer = Random.Range(doorInterval * 0.5f, doorInterval);
        _attackTimer = attackInterval;

        var spawnGO = GameObject.FindWithTag("GhostSpawn");
        if (spawnGO != null) _cachedSpawn = spawnGO.transform;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { enabled = false; return; }
        StartCoroutine(StartAI());
    }

    private IEnumerator StartAI()
    {
        yield return new WaitForSeconds(0.5f);

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"[GhostAI] ke NavMesh: {hit.position}");
            }
            else Debug.LogError("[GhostAI] ga bisa ktmu NavMesh pos");
        }

        MakeInvisible();
        StartRoam();
    }

     protected virtual void Update()
    {
        if (!IsServer) return;

        TickTimers();
        CheckWaterFall();

        _stateCheckTimer -= Time.deltaTime;
        if (_stateCheckTimer <= 0f)
        {
            _stateCheckTimer = StateCheckInterval;
            EvaluateState();
        }

        switch (state)
        {
            case AIState.Roaming: UpdateRoam(); break;
            case AIState.Disturbing: UpdateDisturb(); break;
            case AIState.Hunting: UpdateHunt(); break;
        }

        if (animator != null)
            animator.SetFloat("Speed", agent.velocity.magnitude);
    }

    private void TickTimers()
    {
        _appearTimer -= Time.deltaTime;
        _screamTimer -= Time.deltaTime;
        _teleportTimer -= Time.deltaTime;
        _disturbTimer -= Time.deltaTime;
        _lightTimer -= Time.deltaTime;
        _doorTimer -= Time.deltaTime;
        _attackTimer -= Time.deltaTime;
    }

    private void EvaluateState()
    {
        int p = Phase;
        if (p >= 2)
        {
            var plate = FindPlateWithItem();
            if (plate != null)
            {
                _targetPlate = plate;
                _disturbTimer = disturbInterval;
                SetState(AIState.Disturbing);
                return;
            }
        }

        if (p >= 3)
        {
            var target = FindExorcist();
            if (target != null)
            {
                currentTarget = target;
                SetState(AIState.Hunting);
                return;
            }
        }

        if (state == AIState.Hunting && currentTarget == null)
            SetState(AIState.Roaming);
        else if (state != AIState.Disturbing && state != AIState.Hunting)
            SetState(AIState.Roaming);
    }

    private void SetState(AIState next)
    {
        if (state == next) return;
        Debug.Log($"[GhostAI] State: {state} ke {next}");
        state = next;

        if (next == AIState.Roaming) StartRoam();
        else StopRoam();
    }

    private void StartRoam()
    {
        StopRoam();
        _roamCoroutine = StartCoroutine(RoamRoutine());
    }

    private void StopRoam()
    {
        if (_roamCoroutine == null) return;
        StopCoroutine(_roamCoroutine);
        _roamCoroutine = null;
    }

    private IEnumerator RoamRoutine()
    {
        while (state == AIState.Roaming)
        {
            yield return new WaitUntil(() =>
                agent != null && agent.isOnNavMesh && agent.isActiveAndEnabled);

            Vector3 dest;

            if (Phase >= 3 && _learnedPositions.Count > 0)
            {
                dest = _learnedPositions[Random.Range(0, _learnedPositions.Count)];
                Debug.Log("[GhostAI] Phase 3 wander ke learned pos ");
            }
            else
            {
                dest = GetRoamPoint();
            }

            if (agent.isOnNavMesh)
                agent.SetDestination(dest);

            yield return new WaitUntil(() =>
                state != AIState.Roaming ||
                (!agent.pathPending && agent.remainingDistance < 0.5f));

            if (state != AIState.Roaming) yield break;

            float wait = Phase >= 3? Random.Range(0.5f, 1.5f)  : Random.Range(roamWaitMin, roamWaitMax);

            yield return new WaitForSeconds(wait);
        }
    }

    private void UpdateRoam()
    {
        _learnTimer -= Time.deltaTime;
        if (_learnTimer <= 0f)
        {
            _learnTimer = 5f;
            SampleExorcistPositions();
        }

        TryPhase1Skills();

        if (Phase >= 2)
        {
            if (_lightTimer <= 0f)
            {
                TryToggleNearbyLight();
                _lightTimer = lightInterval;
            }
            if (_doorTimer <= 0f)
            {
                TryToggleNearbyDoor();
                _doorTimer = doorInterval;
            }
        }
    }

    private void TryPhase1Skills()
    {
        if (_appearTimer <= 0f)
        {
            _appearTimer = Random.Range(appearInterval * 0.8f, appearInterval * 1.2f);
            TriggerAppearClientRpc();
            Debug.Log("[GhostAI] Appear");
        }

        if (_screamTimer <= 0f)
        {
            _screamTimer = Random.Range(screamInterval * 0.8f, screamInterval * 1.2f);
            TriggerScreamClientRpc();
            Debug.Log("[GhostAI] Scream");
        }

        if (_teleportTimer <= 0f)
        {
            _teleportTimer = Random.Range(teleportInterval * 0.8f, teleportInterval * 1.2f);
            TeleportRandom();
        }
    }

    private Vector3 GetRoamPoint()
    {
        if (roamPoints == null || roamPoints.Length == 0)
            TryAutoFindRoamPoints();

        if (roamPoints != null && roamPoints.Length > 0)
        {
            var valid = System.Array.FindAll(roamPoints, t => t != null);
            if (valid.Length > 0)
                return valid[Random.Range(0, valid.Length)].position;
        }

        Vector3 rand = transform.position + Random.insideUnitSphere * roamRadius;
        rand.y = transform.position.y;
        if (NavMesh.SamplePosition(rand, out var hit, roamRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;
    }

    private void TryAutoFindRoamPoints()
    {
        var tagged = GameObject.FindGameObjectsWithTag("GhostRoamPoint");

        if (tagged.Length == 0) {
            Debug.LogWarning("[GhostAI] ga ada GhostRoamPoin");
            return;
        }
        roamPoints = new Transform[tagged.Length];
        for (int i = 0; i < tagged.Length; i++)
        {
            roamPoints[i] = tagged[i].transform;

        }
        Debug.Log($"[GhostAI] ketemu {roamPoints.Length} roam points.");
    }

    private void UpdateDisturb()
    {
        TryPhase1Skills();

        if (_targetPlate == null || _targetPlate.IsEmpty())
        {
            _targetPlate = null;
            SetState(AIState.Roaming);
            return;
        }

        if (agent.isOnNavMesh)
            agent.SetDestination(_targetPlate.transform.position);

        if (Vector3.Distance(transform.position, _targetPlate.transform.position) < interactRadius)
        {
            DisturbPlate(_targetPlate);
            _targetPlate = null;
            SetState(AIState.Roaming);
        }

        if (_lightTimer <= 0f) TryToggleNearbyLight();
        if (_doorTimer <= 0f) TryToggleNearbyDoor();
    }

    private void DisturbPlate(Plate plate)
    {
        var item = plate.GetCurrentItem();
        if (item == null) return;

        var no = item.GetComponent<NetworkObject>();
        if (no == null) return;

        DropItemServerSide(plate, no.NetworkObjectId, transform.position);
        Debug.Log($"[GhostAI] ganggu plate trs drop {item.name}");
    }

    private void DropItemServerSide(Plate plate, ulong itemNetId, Vector3 dropPos)
    {
        plate.RemoveItem();

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null) return;

        item.transform.position = dropPos + Vector3.up * 0.5f;
        item.StopFollow();

        var rb = item.GetComponent<Rigidbody>();

        var col = item.GetComponent<Collider>();
        
        if (rb != null) { 
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero; 
        }
        if (col != null)
        {
            col.isTrigger = false;
        }

        SyncDropItemClientRpc(itemNetId, dropPos + Vector3.up * 0.5f);
    }
    private void TryToggleNearbyLight()
    {
        var lights = FindObjectsByType<NetworkLight>(FindObjectsSortMode.None);
        foreach (var nl in lights)
        {
            float dist = Vector3.Distance(transform.position, nl.transform.position);
            if (dist > interactRadius) continue;

            var no = nl.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;

            nl.ToggleLightServerRpc();
            _lightTimer = lightInterval;
            return;
        }
    }
    [ClientRpc]
    private void SyncDropItemClientRpc(ulong itemNetId, Vector3 pos)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null) return;

        item.transform.position = pos;
        item.StopFollow();

        var rb = item.GetComponent<Rigidbody>();
        var col = item.GetComponent<Collider>();
        if (rb != null) { 
            rb.isKinematic = false; 
            rb.linearVelocity = Vector3.zero;
        }
        if (col != null)
        {
            col.isTrigger = false;
        }
    }

    private void TryToggleNearbyDoor()
    {
        foreach (var c in Physics.OverlapSphere(transform.position, interactRadius))
        {
            var door = c.GetComponentInParent<Door>();
            if (door == null) continue;

            var no = door.GetComponent<NetworkObject>();
            if (no == null || !no.IsSpawned) continue;

            door.ToggleDoorServerRpc();
            _doorTimer = doorInterval;
            Debug.Log("[GhostAI] Interact door");
            return;
        }
    }

    private void UpdateHunt()
    {
        TryPhase1Skills();

        if (currentTarget == null ||
            currentTarget.GetComponent<HealthSystem>()?.IsDead() == true)
        {
            currentTarget = null;
            SetState(AIState.Roaming);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        bool canSee = dist <= sightRange && HasLineOfSight(currentTarget.transform);

        if (canSee)
        {
            if (agent.isOnNavMesh)
                agent.SetDestination(currentTarget.transform.position);

            if (dist <= attackRange && _attackTimer <= 0f)
            {
                _attackTimer = attackInterval;
                var no = currentTarget.GetComponent<NetworkObject>();
                if (no != null)
                {
                    DoAttack(no.NetworkObjectId);
                    Debug.Log($"[GhostAI] Attack");
                }
            }
        }
        else
        {
            AddLearnedPosition(currentTarget.transform.position);
            currentTarget = null;
            SetState(AIState.Roaming);
        }
    }
    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 direction = (target.position + Vector3.up * 1f) - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, sightRange))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
                return true;

            return false;
        }

        return false;
    }
    private void DoAttack(ulong exorcistNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(exorcistNetId, out var no)) return;

        var health = no.GetComponent<HealthSystem>();
        if (health == null || health.IsDead()) return;

        health.TakeDamageServerRpc(attackDamage);
        TriggerAttackAnimClientRpc();
    }

    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    {
        if (animator != null) animator.SetTrigger("Attack");
    }

    private void SampleExorcistPositions()
    {
        foreach (var pn in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
        {
            if (pn.role.Value != PlayerRole.Exorcist) continue;
            AddLearnedPosition(pn.transform.position);
        }
    }

    private void AddLearnedPosition(Vector3 pos)
    {
        _learnedPositions.Add(pos);
        if (_learnedPositions.Count > 20)
            _learnedPositions.RemoveRange(0, 5);
    }
    private Plate FindPlateWithItem()
    {
        foreach (var p in FindObjectsByType<Plate>(FindObjectsSortMode.None))
            if (!p.IsEmpty()) return p;
        return null;
    }

    private PlayerMovement FindExorcist()
    {
        PlayerMovement closest = null;
        float closestDist = sightRange;

        foreach (var pn in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
        {
            if (pn.role.Value != PlayerRole.Exorcist) continue;

            var pm = pn.GetComponent<PlayerMovement>();
            if (pm == null) continue;
            if (pm.GetComponent<HealthSystem>()?.IsDead() == true) continue;

            float d = Vector3.Distance(transform.position, pm.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                closest = pm;
            }
        }

        return closest;
    }

    private void CheckWaterFall()
    {
        if (waterLayer == 0) return;
        if (!Physics.CheckSphere(transform.position, 0.5f, waterLayer)) return;

        Vector3 dest = GetFallbackPosition();
        if (NavMesh.SamplePosition(dest, out var hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            SyncPositionClientRpc(hit.position);
            Debug.Log("[GhostAI] Jatuh ke air trs teleport ke spawn");
        }
    }

    private Vector3 GetFallbackPosition()
    {
        if (_cachedSpawn == null)
        {
            var go = GameObject.FindWithTag("GhostSpawn");
            if (go != null) _cachedSpawn = go.transform;
        }
        if (_cachedSpawn != null) return _cachedSpawn.position;
        return transform.position + Vector3.up * 3f;
    }

    private void TeleportRandom()
    {
        Vector3 rand = transform.position + Random.insideUnitSphere * roamRadius * 2f;
        rand.y = transform.position.y;

        if (NavMesh.SamplePosition(rand, out var hit, roamRadius * 2f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            SyncPositionClientRpc(hit.position);
            Debug.Log($"[GhostAI] Teleport ke {hit.position}");
        }
    }

    [ClientRpc]
    private void SyncPositionClientRpc(Vector3 pos)
    {
        if (!IsServer)
        {
            transform.position = pos;
        }
    }

    [ClientRpc]
    protected void TriggerAppearClientRpc()
    {
        StartCoroutine(AppearBriefly());
    }

    private IEnumerator AppearBriefly()
    {
        SetVisibleLocal(true);
        yield return new WaitForSeconds(1f);
        SetVisibleLocal(false);
    }

    private void SetVisibleLocal(bool visible)
    {
        if (IsServer)
            GetComponent<GhostVisibilityManager>()?.SetVisibleServer(visible);
        else
            RequestSetVisibleServerRpc(visible);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSetVisibleServerRpc(bool visible)
        => GetComponent<GhostVisibilityManager>()?.SetVisibleServer(visible);

    [ClientRpc]
    private void TriggerScreamClientRpc()
    {
        if (screamAudioSource != null && screamClip != null)
            screamAudioSource.PlayOneShot(screamClip);
    }

    private void MakeInvisible()
        => GetComponent<GhostVisibilityManager>()?.SetVisibleServer(false);

    protected virtual void ApplyPassive() { }

    public void SetGhostType(GhostType gt) { ghostType = gt; }
}