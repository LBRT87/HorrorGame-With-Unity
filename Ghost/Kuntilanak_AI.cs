using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class KuntilanakAI : GhostAIBase
{
    [Header("Kuntilanak AI - Slow Aura")]
    public float slowRadius = 5f;
    public float slowAmount = 0.5f;

    [Header("Fly Warp (Phase 2+)")]
    public float flyWarpInterval = 20f;
    public Transform[] flyWarpPoints;

    private float _flyWarpTimer;
    private float _slowTickTimer = 0.5f;

    private Dictionary<PlayerMovement, float> _originalSpeeds
        = new Dictionary<PlayerMovement, float>();

    protected override void Awake()
    {
        base.Awake();
        ghostType = GhostType.Kuntilanak;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _flyWarpTimer = flyWarpInterval;

        StartCoroutine(EnsureValidNavMeshPosition());
    }

    private IEnumerator EnsureValidNavMeshPosition()
    {
        yield return new WaitForSeconds(0.3f);

        if (agent == null || agent.isOnNavMesh) yield break;

        if (NavMesh.SamplePosition(transform.position, out var hit, 10f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            Debug.Log($"[KuntilanakAI] Fix spawn NavMesh {hit.position}");
        }
        else
        {
            var spawnGO = GameObject.FindWithTag("GhostSpawn");
            if (spawnGO != null && NavMesh.SamplePosition(spawnGO.transform.position, out var spawnHit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(spawnHit.position);
                Debug.Log($"[KuntilanakAI] Warp ke GhostSpawn  {spawnHit.position}");
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer) return;

        if (Phase >= 2)
        {
            _flyWarpTimer -= Time.deltaTime;
            if (_flyWarpTimer <= 0f)
            {
                _flyWarpTimer = flyWarpInterval;
                TryFlyWarp();
            }
        }

        _slowTickTimer -= Time.deltaTime;
        if (_slowTickTimer <= 0f)
        {
            _slowTickTimer = 0.5f;
            ApplySlowAura();
        }
    }

    private void TryFlyWarp()
    {
        if (flyWarpPoints == null || flyWarpPoints.Length == 0) return;

        Transform target = flyWarpPoints[Random.Range(0, flyWarpPoints.Length)];
        if (target == null) return;

        if (NavMesh.SamplePosition(target.position, out var hit, 5f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            TriggerAppearClientRpc();
            Debug.Log($"[KuntilanakAI] FlyWarp → {hit.position}");
        }
    }

    private void ApplySlowAura()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        bool anyNearby = false;

        foreach (var p in players)
        {
            if (p == null) continue;

            var no = p.GetComponent<NetworkObject>();
            if (no == null) continue;

            float dist = Vector3.Distance(transform.position, p.transform.position);

            if (dist <= slowRadius)
            {
                anyNearby = true;

                if (!_originalSpeeds.ContainsKey(p))
                    _originalSpeeds[p] = p.speed;

                float slowed = _originalSpeeds[p] * slowAmount;
                if (p.speed > slowed + 0.01f)
                    ApplySlowClientRpc(no.NetworkObjectId, slowed);
            }
            else
            {
                if (_originalSpeeds.TryGetValue(p, out float original))
                {
                    _originalSpeeds.Remove(p);
                    RestoreSpeedClientRpc(no.NetworkObjectId, original);
                }
            }
        }
    }

    [ClientRpc]
    private void ApplySlowClientRpc(ulong exorcistNetId, float slowedSpeed)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;
        var p = no.GetComponent<PlayerMovement>();
        if (p != null) p.speed = slowedSpeed;
    }

    [ClientRpc]
    private void RestoreSpeedClientRpc(ulong exorcistNetId, float originalSpeed)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;
        var p = no.GetComponent<PlayerMovement>();
        if (p != null) p.speed = originalSpeed;
    }

  
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            foreach (var kvp in _originalSpeeds)
            {
                if (kvp.Key != null)
                {
                    var no = kvp.Key.GetComponent<NetworkObject>();
                    if (no != null)
                        RestoreSpeedClientRpc(no.NetworkObjectId, kvp.Value);
                }
            }
            _originalSpeeds.Clear();
        }
        base.OnNetworkDespawn();
    }

    protected override void ApplyPassive() { }
}