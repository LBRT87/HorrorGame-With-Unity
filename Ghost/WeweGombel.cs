using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class WeweGombel : GhostBasic
{
    public float kidnapDuration = 10f;

    public float holdRequired = 10f;

    public Transform handTransform;

    private NetworkVariable<ulong> _kidnapTargetId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerMovement _kidnapTargetLocal = null;
    private float _kidnapTimer = 0f;
    private float _holdTimer = 0f;
    private bool _holdActive = false;

    protected override void Awake()
    {
        base.Awake();
        ghostType = GhostType.WeweGombel;
        attackDamage = 2;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _kidnapTargetId.OnValueChanged += OnKidnapChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _kidnapTargetId.OnValueChanged -= OnKidnapChanged;
    }

    private void OnKidnapChanged(ulong _, ulong newId)
    {
        if (newId == ulong.MaxValue && IsOwner)
        {
            _kidnapTargetLocal = null;
            _kidnapTimer = 0f;
            ghostHud?.HideHoldProgress();
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsOwner) return;

        if (_kidnapTargetLocal != null)
        {
            _kidnapTimer -= Time.deltaTime;

            float progress = Mathf.Clamp01(1f - (_kidnapTimer / kidnapDuration));
            ghostHud?.ShowHoldProgress(progress);

            Transform holdPos = handTransform != null ? handTransform : transform;
            Vector3 targetPos = holdPos.position + transform.right * 1.2f;
            UpdateKidnapPositionServerRpc(
                _kidnapTargetId.Value, targetPos);

            if (_kidnapTimer <= 0f)
                ReleaseKidnapServerRpc();
        }

        HandleKidnapHold();
    }

    [ServerRpc]
    private void UpdateKidnapPositionServerRpc(ulong exorcistNetId, Vector3 position)
    {
        UpdateKidnapPositionClientRpc(exorcistNetId, position);
    }

    [ClientRpc]
    private void UpdateKidnapPositionClientRpc(ulong exorcistNetId, Vector3 position)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;

        var pm = no.GetComponent<PlayerMovement>();
        if (pm == null) return;

        var cc = no.GetComponent<CharacterController>();
        if (cc != null && cc.enabled)
        {
            cc.enabled = false;
            no.transform.position = position;
            cc.enabled = true;
        }
        else
        {
            no.transform.position = position;
        }
    }

    private void HandleKidnapHold()
    {
        if (nearbyExorcist == null || _kidnapTargetId.Value != ulong.MaxValue) return;
        if (CurrentPhase < 2) return;

        if (ghostInputHandler.IsSpecialHeld)
        {
            _holdTimer += Time.deltaTime;
            _holdActive = true;
            ghostHud?.ShowHoldProgress(Mathf.Clamp01(_holdTimer / holdRequired));

            if (_holdTimer >= holdRequired)
            {
                _holdTimer = 0f;
                _holdActive = false;
                ghostHud?.HideHoldProgress();

                if (_specialTimer <= 0f && TryUseSpecialSkillNearby())
                {
                    _specialTimer = specialSkillCooldown;
                    ghostHud?.StartCooldown(GhostHUD.SkillType.Special);
                }
            }
        }
        else if (_holdActive)
        {
            _holdTimer = 0f;
            _holdActive = false;
            ghostHud?.HideHoldProgress();
        }
    }

    protected override bool TryUseSpecialSkillNearby()
    {
        if (nearbyExorcist == null) return false;
        if (_kidnapTargetId.Value != ulong.MaxValue) return false;

        var no = nearbyExorcist.GetComponent<NetworkObject>();
        if (no == null) return false;

        KidnapServerRpc(no.NetworkObjectId);
        return true;
    }

    [ServerRpc]
    private void KidnapServerRpc(ulong exorcistNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;
        if (no.GetComponent<PlayerMovement>() == null) return;

        _kidnapTargetId.Value = exorcistNetId;

        FreezeExorcistClientRpc(exorcistNetId, true);
        TriggerKidnapAnimClientRpc(exorcistNetId);

        NotifyKidnapClientRpc(exorcistNetId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    private void FreezeExorcistClientRpc(ulong exorcistNetId, bool freeze)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;

        if (no.IsOwner)
        {
            var cc = no.GetComponent<CharacterController>();
            var input = no.GetComponent<PlayerInputHandler>();
            if (cc != null) cc.enabled = !freeze;
            if (input != null) input.enabled = !freeze;
        }
    }

    [ClientRpc]
    private void TriggerKidnapAnimClientRpc(ulong exorcistNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;

        var anim = no.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("isLying", true);
            anim.SetBool("isMoving", false);
        }
    }

    [ClientRpc]
    private void NotifyKidnapClientRpc(ulong id, ClientRpcParams _ = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(id, out var no)) return;

        _kidnapTargetLocal = no.GetComponent<PlayerMovement>();
        _kidnapTimer = kidnapDuration;
        Debug.Log("[WeweGombel] Kidnap: " + _kidnapTargetLocal?.name);
    }

    [ServerRpc]
    private void ReleaseKidnapServerRpc()
    {
        ulong tid = _kidnapTargetId.Value;
        if (tid == ulong.MaxValue) return;

        FreezeExorcistClientRpc(tid, false);
        RestoreKidnapAnimClientRpc(tid);
        _kidnapTargetId.Value = ulong.MaxValue;

        ReleaseLocalClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    private void RestoreKidnapAnimClientRpc(ulong exorcistNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(exorcistNetId, out var no)) return;

        var anim = no.GetComponent<Animator>();
        if (anim != null) anim.SetBool("isLying", false);
    }

    [ClientRpc]
    private void ReleaseLocalClientRpc(ClientRpcParams _ = default)
    {
        _kidnapTargetLocal = null;
        _kidnapTimer = 0f;
        ghostHud?.HideHoldProgress();
    }

    protected override void HandleAttack() => base.HandleAttack();

    public override void ApplyPassiveSkillToGhost(PlayerMovement target) { }
}