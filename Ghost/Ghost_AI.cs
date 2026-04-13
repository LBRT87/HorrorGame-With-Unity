using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GhostAI : NetworkBehaviour
{
    public GhostType ghostType;

    [Header("Stats")]
    public float roamRadius = 20f;
    public float attackRange = 2f;
    public float attackDamage = 1f;
    public float attackCooldown = 3f;

    [Header("Timers")]
    public float appearDuration = 1f;
    public float teleportCooldown = 20f;

    private NavMeshAgent agent;
    private Transform playerTransform;
    private HealthSystem playerHealth;

    private int currentPhase = 1;
    private float attackTimer;
    private float teleportTimer;

    private Dictionary<Vector3, int> heatmap = new Dictionary<Vector3, int>();
    private float learnTimer = 5f;

    private float damageMultiplier = 1f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        FindPlayer();

        switch (ghostType)
        {
            case GhostType.Tuyul: agent.speed *= 1.5f; break;
            case GhostType.WeweGombel: damageMultiplier = 2f; break;
        }

        StartCoroutine(PhaseRoutine());
    }

    private void FindPlayer()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        if (players.Length > 0)
        {
            playerTransform = players[0].transform;
            playerHealth = players[0].GetComponent<HealthSystem>();
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        currentPhase = GamePhaseManager.Instance?.currentPhase.Value ?? 1;

        attackTimer -= Time.deltaTime;
        teleportTimer -= Time.deltaTime;

        HandleLearning();

        if (currentPhase >= 3)
            HandleHunting();
    }
    private void HandleLearning()
    {
        if (playerTransform == null) return;

        learnTimer -= Time.deltaTime;

        if (learnTimer <= 0f)
        {
            learnTimer = 5f;

            Vector3 pos = playerTransform.position;
            pos = new Vector3(Mathf.Round(pos.x), 0, Mathf.Round(pos.z));

            if (heatmap.ContainsKey(pos))
                heatmap[pos]++;
            else
                heatmap[pos] = 1;
        }
    }

    private Vector3 GetHotspot()
    {
        int max = 0;
        Vector3 best = transform.position;

        foreach (var kvp in heatmap)
        {
            if (kvp.Value > max)
            {
                max = kvp.Value;
                best = kvp.Key;
            }
        }

        return best;
    }
    private IEnumerator PhaseRoutine()
    {
        while (true)
        {
            currentPhase = GamePhaseManager.Instance?.currentPhase.Value ?? 1;

            switch (currentPhase)
            {
                case 1:
                    yield return StartCoroutine(RoamRoutine());
                    break;

                case 2:
                    yield return StartCoroutine(DisturbRoutine());
                    break;

                case 3:
                    yield return null;
                    break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator RoamRoutine()
    {
        Vector3 target = GetRandomNavPoint();
        agent.SetDestination(target);

        yield return new WaitUntil(() =>
            !agent.pathPending && agent.remainingDistance < 1f);

        if (teleportTimer <= 0f)
        {
            teleportTimer = teleportCooldown;
            TeleportRandom();
        }
    }

    private IEnumerator DisturbRoutine()
    {
        Plate[] plates = FindObjectsByType<Plate>(FindObjectsSortMode.None);

        Plate targetPlate = null;
        foreach (var p in plates)
        {
            if (!p.IsEmpty())
            {
                targetPlate = p;
                break;
            }
        }

        if (targetPlate != null)
        {
            agent.SetDestination(targetPlate.transform.position);

            yield return new WaitUntil(() =>
                !agent.pathPending &&
                Vector3.Distance(transform.position, targetPlate.transform.position) < 2f);

            ItemPickUp item = targetPlate.GetCurrentItem();
            targetPlate.RemoveItem();

            if (item != null)
            {
                item.ThrowFromGhostClientRpc(Random.insideUnitSphere * 5f);
            }
        }
        else
        {
            yield return StartCoroutine(RoamRoutine());
        }
    }

    private void HandleHunting()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (CanSeePlayer())
        {
            agent.SetDestination(playerTransform.position);

            if (dist <= attackRange && attackTimer <= 0f)
            {
                attackTimer = attackCooldown;
                DealDamage();
            }
        }
        else
        {
            Vector3 dest = GetHotspot();
            agent.SetDestination(dest);
        }
    }

    private bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        return dist < 20f;
    }

    private void DealDamage()
    {
        if (playerHealth == null) return;

        int dmg = Mathf.RoundToInt(attackDamage * damageMultiplier);
        playerHealth.TakeDamageServerRpc(dmg);

        Debug.Log($"[GhostAI] Attack dmg={dmg}");
    }

    private void TeleportRandom()
    {
        Vector3 pos = GetRandomNavPoint();
        agent.Warp(pos);
    }

    private Vector3 GetRandomNavPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * roamRadius + transform.position;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }
}