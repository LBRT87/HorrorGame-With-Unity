using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class Tuyul : GhostBasic
{
    public float stolenItemDuration = 10f;

    public float holdRequired = 10f;

    private ItemPickUp stolenItem = null;
    private float stolenTimer = 0f;
    private float _holdTimer = 0f;
    private bool _holdActive = false;
    private ulong stolenItemId = ulong.MaxValue;

    public Transform handTransform;

    protected override void Awake()
    {
        base.Awake();
        ghostType = GhostType.Tuyul;
        speedMove *= 1.5f;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsOwner) return;

        if (stolenItem != null)
        {
            stolenTimer -= Time.deltaTime;

            if (handTransform != null)
            {
                stolenItem.transform.position = handTransform.position;
            }

            if (stolenTimer <= 0f)
            {
                var no = stolenItem.GetComponent<NetworkObject>();
                if (no != null)
                {
                    AutoDropServerRpc(no.NetworkObjectId);
                }
                ClearStolen();
            }
        }

        HandleSpecialHold();
    }

    private void HandleSpecialHold()
    {
        if (nearbyExorcist == null || stolenItem != null) return;

        if (ghostInputHandler.IsSpecialHeld && CurrentPhase >= 2)
        {
            _holdTimer += Time.deltaTime;
            _holdActive = true;

            float prog = Mathf.Clamp01(_holdTimer / holdRequired);
            ghostHud?.ShowHoldProgress(prog);

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
        if (stolenItem != null) return false;
        if (nearbyExorcist == null) return false;

        ItemPickUp item = GetExorcistHeldItem(nearbyExorcist);
        if (item == null) return false;

        if (IsNonStealable(item))
        {
            ghostHud?.OpenMessagePanel("Item ini tidak bisa dicuri!");
            return false;
        }

        var itemNetObj = item.GetComponent<NetworkObject>();
        if (itemNetObj == null) return false;

        StealItemServerRpc(itemNetObj.NetworkObjectId);
        return true;
    }

    private bool IsNonStealable(ItemPickUp item)
    {
        var t = item.itemType;
        return t == ItemType.Torch || t == ItemType.Spiritbox || t == ItemType.Notebook;
    }

    private ItemPickUp GetExorcistHeldItem(PlayerMovement exorcist)
    {
        if (exorcist.Hand == null) return null;
        Collider[] hits = Physics.OverlapSphere(exorcist.Hand.position, 0.5f);
        foreach (var hit in hits)
        {
            var item = hit.GetComponent<ItemPickUp>();
            if (item != null && item.isPicked) return item;
        }
        return null;
    }

    [ServerRpc]
    private void StealItemServerRpc(ulong itemNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null || !item.isPicked) return;

        ulong ghostNetObjId = GetComponent<NetworkObject>().NetworkObjectId;

        item.ForceDropServerRpc();
        StartCoroutine(AttachAfterDropDelay(itemNetId, ghostNetObjId));
    }

    private IEnumerator AttachAfterDropDelay(ulong itemNetId, ulong ghostNetObjId)
    {
        yield return new WaitForSeconds(0.1f);

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) yield break;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null) yield break;

        item.AttachToGhostHandClientRpc(ghostNetObjId);

        NotifyStealClientRpc(itemNetId, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    private void NotifyStealClientRpc(ulong itemNetId, ClientRpcParams _ = default)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        stolenItem = no.GetComponent<ItemPickUp>();
        stolenTimer = stolenItemDuration;
        Debug.Log("[Tuyul] Steal berhasil: " + stolenItem?.name);
    }

    [ServerRpc]
    private void AutoDropServerRpc(ulong itemNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null) return;

        item.ThrowFromGhostClientRpc(
            Vector3.down * 2f + new Vector3(
                Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)));
    }

    private void ClearStolen()
    {
        stolenItem = null;
        stolenTimer = 0f;
    }

    protected override void HandleAttack()
    {
        if (animator != null) animator.SetTrigger("Attack");
        base.HandleAttack();
    }

    public override void ApplyPassiveSkillToGhost(PlayerMovement target) { }
}