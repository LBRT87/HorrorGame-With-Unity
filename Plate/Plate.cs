using Unity.Netcode;
using UnityEngine;

public class Plate : NetworkBehaviour
{
    private readonly NetworkVariable<ulong> _currentItemNetId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsEmpty() => _currentItemNetId.Value == ulong.MaxValue;
    public bool HasItem() => _currentItemNetId.Value != ulong.MaxValue;

    public ItemPickUp GetCurrentItem()
    {
        if (_currentItemNetId.Value == ulong.MaxValue) return null;

        if (NetworkManager.Singleton == null) return null;
        if (NetworkManager.Singleton.SpawnManager == null) return null;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(_currentItemNetId.Value, out var netObj))
        {
            if (netObj == null) return null;
            return netObj.GetComponent<ItemPickUp>();
        }

        if (IsServer) _currentItemNetId.Value = ulong.MaxValue;
        return null;
    }


    public void PlaceItem(ItemPickUp item)
    {
        if (!IsServer || item == null || HasItem()) return;
        var netObj = item.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return;

        _currentItemNetId.Value = netObj.NetworkObjectId;
        PositionItemOnPlate(netObj.gameObject); 
        PlaceItemVisualClientRpc(netObj);
    }


    public void RequestPlaceItem(ItemPickUp item)
    {
        if (item == null) return;
        var netObj = item.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned)
        {
            Debug.LogWarning("[Plate] RequestPlaceItem: item belum di-spawn!");
            return;
        }
        if (!IsSpawned)
        {
            Debug.LogWarning("[Plate] RequestPlaceItem: Plate belum di-spawn!");
            return;
        }
        PlaceItemServerRpc(netObj);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlaceItemServerRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject netObj))
        {
            Debug.LogWarning("[Plate] PlaceItemServerRpc: gagal resolve NetworkObjectReference");
            return;
        }
        if (HasItem()) return;
        var itemPickUp = netObj.GetComponent<ItemPickUp>();
        if (itemPickUp != null && itemPickUp.isPicked)
            itemPickUp.ForceDropServerRpc();
        _currentItemNetId.Value = netObj.NetworkObjectId;

        PositionItemOnPlate(netObj.gameObject);

        PlaceItemVisualClientRpc(netObj);
    }

    [ClientRpc]
    private void PlaceItemVisualClientRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject netObj)) return;

        Vector3 platePos = transform.position;
        netObj.transform.position = new Vector3(platePos.x, platePos.y + 0.3f, platePos.z);
        netObj.transform.rotation = Quaternion.identity;

        var rb = netObj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        var cols = netObj.GetComponents<Collider>();
        foreach (var col in cols)
            col.enabled = false;
    }
    private void PositionItemOnPlate(GameObject itemGO)
    {
        Vector3 platePos = transform.position;
        itemGO.transform.position = new Vector3(platePos.x, platePos.y + 0.3f, platePos.z);
        itemGO.transform.rotation = Quaternion.identity;

        var rb = itemGO.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        var cols = itemGO.GetComponents<Collider>();
        foreach (var col in cols)
            col.enabled = false;
    }


    public void RequestTakeItem(ulong clientId)
    {
        if (!IsSpawned) return;
        TakeItemServerRpc(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeItemServerRpc(ulong clientId)
    {
        if (!HasItem()) return;

        var item = GetCurrentItem();
        _currentItemNetId.Value = ulong.MaxValue;

        if (item == null) return;

        ResetItemPhysicsClientRpc(item.GetComponent<NetworkObject>());

        item.RequestPickUp(clientId);
    }
    [ClientRpc]
    private void ResetItemPhysicsClientRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject netObj)) return;

        var rb = netObj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        var cols = netObj.GetComponents<Collider>();
        foreach (var col in cols)
            col.enabled = true;
    }


    public void RemoveItem()
    {
        if (!IsServer || !HasItem()) return;
        var item = GetCurrentItem();
        if (item != null)
            RemoveItemClientRpc(item.GetComponent<NetworkObject>());
        _currentItemNetId.Value = ulong.MaxValue;
    }

    [ClientRpc]
    private void RemoveItemClientRpc(NetworkObjectReference itemRef)
    {
        if (!itemRef.TryGet(out NetworkObject netObj)) return;

        var rb = netObj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        var cols = netObj.GetComponents<Collider>();
        foreach (var col in cols)
            col.enabled = true;
    }


    public void ThrowItemOff()
    {
        if (!IsServer || !HasItem()) return;

        var item = GetCurrentItem();
        _currentItemNetId.Value = ulong.MaxValue;
        if (item == null) return;

        Vector3 randomDir = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.5f, 1f),
            Random.Range(-1f, 1f)).normalized;

        ThrowItemOffClientRpc(item.GetComponent<NetworkObject>(), randomDir * 5f);
        Debug.Log("[Plate] Item dilempar ghost!");
    }

    [ClientRpc]
    private void ThrowItemOffClientRpc(NetworkObjectReference itemRef, Vector3 force)
    {
        if (!itemRef.TryGet(out NetworkObject netObj)) return;
        var rb = netObj.GetComponent<Rigidbody>();
        var col = netObj.GetComponent<Collider>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(force, ForceMode.Impulse);
        }
        if (col != null) col.isTrigger = false;
    }
}