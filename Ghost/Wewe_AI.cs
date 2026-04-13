using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class WeweGombelAI : GhostAIBase
{
    public float kidnapInterval = 35f;
    public float kidnapDuration = 10f;
    public float kidnapRadius = 6f; 

    private float _kidnapTimer;
    private bool _isKidnapping = false;
    private ulong _kidnapTargetNetId = ulong.MaxValue;
    private float _kidnapReleaseTimer = 0f;
    private float _dragSyncTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        ghostType = GhostType.WeweGombel;
        attackDamage = 2; 
    }

    public override void OnNetworkSpawn()
    {   
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _kidnapTimer = kidnapInterval;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer) return;

        if (!_isKidnapping)
        {
            if (Phase >= 2)
            {
                _kidnapTimer -= Time.deltaTime;
                if (_kidnapTimer <= 0f)
                {
                    _kidnapTimer = kidnapInterval;
                    TryKidnapNearby();
                }
            }
        }
        else
        {
            _kidnapReleaseTimer -= Time.deltaTime;

            _dragSyncTimer -= Time.deltaTime;
            if (_dragSyncTimer <= 0f && _kidnapTargetNetId != ulong.MaxValue)
            {
                _dragSyncTimer = 0.1f;
                DragTargetClientRpc(_kidnapTargetNetId, transform.position + transform.right * 1.2f);

                float progress = Mathf.Clamp01(1f - (_kidnapReleaseTimer / kidnapDuration));
                UpdateKidnapProgressClientRpc(progress);
            }

            if (_kidnapReleaseTimer <= 0f)
                ReleaseKidnap();
        }
    }

    private void TryKidnapNearby()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        PlayerMovement target = null;
        float closest = kidnapRadius;

        foreach (var p in players)
        {
            if (p == null) continue;
            var hs = p.GetComponent<HealthSystem>();
            if (hs != null && hs.IsDead()) continue;

            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closest) { closest = d; target = p; }
        }

        if (target == null) return;

        var no = target.GetComponent<NetworkObject>();
        if (no == null) return;

        _kidnapTargetNetId = no.NetworkObjectId;
        _isKidnapping = true;
        _kidnapReleaseTimer = kidnapDuration;

        FreezeExorcistOwnerClientRpc(_kidnapTargetNetId, true);

        TriggerKidnapAnimClientRpc(_kidnapTargetNetId);

        TriggerAppearClientRpc();

        Debug.Log($"[WeweGombelAI] Kidnap {target.name} selama {kidnapDuration}s");
    }

    private void ReleaseKidnap()
    {
        if (_kidnapTargetNetId != ulong.MaxValue)
        {
            FreezeExorcistOwnerClientRpc(_kidnapTargetNetId, false);
            RestoreKidnapAnimClientRpc(_kidnapTargetNetId);
            HideKidnapProgressClientRpc();
        }
        _kidnapTargetNetId = ulong.MaxValue;
        _isKidnapping = false;
        _kidnapReleaseTimer = 0f;
        Debug.Log("[WeweGombelAI] Exorcist dilepas");
    }

    [ClientRpc]
    private void FreezeExorcistOwnerClientRpc(ulong netId, bool freeze)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(netId, out var no)) return;

        if (!no.IsOwner) return;

        var cc = no.GetComponent<CharacterController>();
        var input = no.GetComponent<PlayerInputHandler>();
        if (cc != null) cc.enabled = !freeze;
        if (input != null) input.enabled = !freeze;

        Debug.Log($"[WeweGombelAI] {(freeze ? "Freeze" : "Unfreeze")} exorcist (owner)");
    }

    [ClientRpc]
    private void TriggerKidnapAnimClientRpc(ulong netId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(netId, out var no)) return;

        var anim = no.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isLying", true);
            anim.SetBool("isMoving", false);
        }
    }

    [ClientRpc]
    private void RestoreKidnapAnimClientRpc(ulong netId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(netId, out var no)) return;

        var anim = no.GetComponent<Animator>();
        if (anim != null) anim.SetBool("isLying", false);
    }

    [ClientRpc]
    private void DragTargetClientRpc(ulong netId, Vector3 position)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(netId, out var no)) return;

        if (!no.IsOwner) return;

        var cc = no.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            cc.enabled = false;
            no.transform.position = position;
            cc.enabled = true;
        }
        else no.transform.position = position;
    }

    [ClientRpc]
    private void UpdateKidnapProgressClientRpc(float progress)
    {
        var ghostHUD = FindFirstObjectByType<GhostHUD>();
        ghostHUD?.ShowHoldProgress(progress);
    }

    [ClientRpc]
    private void HideKidnapProgressClientRpc()
    {
        var ghostHUD = FindFirstObjectByType<GhostHUD>();
        ghostHUD?.HideHoldProgress();
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer && _isKidnapping && _kidnapTargetNetId != ulong.MaxValue)
        {
            FreezeExorcistOwnerClientRpc(_kidnapTargetNetId, false);
            RestoreKidnapAnimClientRpc(_kidnapTargetNetId);
        }
        base.OnNetworkDespawn();
    }

    protected override void ApplyPassive() { }
}