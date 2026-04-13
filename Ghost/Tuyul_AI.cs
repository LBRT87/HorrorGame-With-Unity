using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TuyulAI : GhostAIBase
{
    public float stealInterval = 25f;
    public float stealRadius = 2f;

    private float _stealTimer;

    protected override void Awake()
    {
        base.Awake();

        if (agent != null)
            agent.speed *= 1.5f;
        ghostType = GhostType.Tuyul;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _stealTimer = stealInterval;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsServer) return;
        if (Phase < 2) return;

        _stealTimer -= Time.deltaTime;
        if (_stealTimer <= 0f)
        {
            _stealTimer = stealInterval;
            TryStealFromNearby();
        }
    }

    private void TryStealFromNearby()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        PlayerMovement closest = null;
        float closestDist = stealRadius;

        foreach (var p in players)
        {
            if (p == null) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < closestDist)
            {
                closest = p;
                closestDist = d;
            }
        }

        if (closest == null) return;

        ItemPickUp heldItem = GetHeldItem(closest);
        if (heldItem == null) return;

        if (IsNonStealable(heldItem)) return;

        var no = heldItem.GetComponent<NetworkObject>();
        if (no == null) return;

        heldItem.ForceDropServerRpc();
        ThrowItemClientRpc(no.NetworkObjectId, transform.position);

        TriggerAppearClientRpc();
    }

    private ItemPickUp GetHeldItem(PlayerMovement exorcist)
    {
        if (exorcist.Hand == null) return null;

        var allItems = FindObjectsByType<ItemPickUp>(FindObjectsSortMode.None);
        foreach (var it in allItems)
        {
            if (it == null || !it.isPicked) continue;
            if (Vector3.Distance(it.transform.position, exorcist.Hand.position) < 0.6f)
                return it;
        }
        return null;
    }

    private bool IsNonStealable(ItemPickUp item)
    {
        var t = item.itemType;
        return t == ItemType.Torch || t == ItemType.Spiritbox || t == ItemType.Notebook;
    }

    [ClientRpc]
    private void ThrowItemClientRpc(ulong itemNetId, Vector3 ghostPos)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(itemNetId, out var no)) return;

        var item = no.GetComponent<ItemPickUp>();
        if (item == null) return;

        item.transform.position = ghostPos + Vector3.up * 0.5f;
        item.StopFollow();

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            Vector3 throwDir = new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)).normalized;
            rb.AddForce(throwDir * 3f, ForceMode.Impulse);
        }

        var col = item.GetComponent<Collider>();
        if (col != null) col.isTrigger = false;
    }



    protected override void ApplyPassive() {  }
}