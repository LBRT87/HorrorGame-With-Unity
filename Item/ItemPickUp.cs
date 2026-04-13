using Unity.Netcode;
using UnityEngine;

public class ItemPickUp : NetworkBehaviour
{
    public ItemType itemType;
    public ItemData data;

    private readonly NetworkVariable<bool> _isPicked =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _heldByClientId =
        new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private Collider col;

    public Sprite itemIcon;

    public bool isPicked => _isPicked.Value;
    public ulong HeldByClientId => _heldByClientId.Value;
    private Transform targetHand;

    public bool isSpecialItem = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    void LateUpdate()
    {
        if (_isPicked.Value && targetHand != null)
        {
            transform.position = targetHand.position;
            transform.rotation = targetHand.rotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        _isPicked.OnValueChanged += (_, picked) =>
        {
            if (!picked)
            {
                targetHand = null;
                if (rb != null) 
                    rb.isKinematic = false;
                if (col != null) 
                    col.isTrigger = false;
            }
        };
    }


    public void RequestPickUp(ulong clientId)
    {
        if (!IsSpawned) return;
        RequestPickUpServerRpc(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPickUpServerRpc(ulong clientId)
    {
        if (_isPicked.Value)
        {
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning($"[ItemPickUp] ClientId {clientId} gada");
            return;
        }

        var playerObj = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (playerObj == null) { Debug.LogWarning("[ItemPickUp] PlayerObject null"); return; }

        var playerMovement = playerObj.GetComponent<PlayerMovement>();
        if (playerMovement == null || playerMovement.Hand == null)
        {
            Debug.LogWarning("[ItemPickUp] Hand null");
            return;
        }

        NetworkObject.ChangeOwnership(clientId);
        _isPicked.Value = true;
        _heldByClientId.Value = clientId;

        PerformPickUpClientRpc(playerObj.NetworkObjectId);
    }

    [ClientRpc]
    private void PerformPickUpClientRpc(ulong playerNetworkId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(playerNetworkId, out var playerNetObj))
        {
            Debug.LogWarning("[ItemPickUp] PlayerNetworkObject tidak ditemukan");
            return;
        }

        var playerMovement = playerNetObj.GetComponent<PlayerMovement>();

        if (playerMovement == null || playerMovement.Hand == null) 
            return;

        targetHand = playerMovement.Hand.transform;
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        if (col != null)
        {
            col.isTrigger = true;
        }
    }


    public void RequestDrop()
    {
        if (!IsSpawned)
        {
            return;
        }
        RequestDropServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDropServerRpc()
    {
        if (!_isPicked.Value) return;
        _isPicked.Value = false;
        _heldByClientId.Value = ulong.MaxValue;
        PerformDropClientRpc();
    }

    [ClientRpc]
    private void PerformDropClientRpc()
    {
        targetHand = null;
        if (rb != null) 
            rb.isKinematic = false;
        if (col != null) 
            col.isTrigger = false;
    }

    public void StopFollow() => targetHand = null;

    public void RequestThrow(Vector3 dir, float force)
    {
        if (!IsSpawned) return;
        RequestThrowServerRpc(dir, force);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestThrowServerRpc(Vector3 dir, float force)
    {
        if (!_isPicked.Value) return;

        ulong throwerClient = _heldByClientId.Value;
        _isPicked.Value = false;
        _heldByClientId.Value = ulong.MaxValue;

        PerformThrowStateClientRpc();
        PerformThrowForceClientRpc(dir, force, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { throwerClient }
            }
        });
    }

    [ClientRpc]
    private void PerformThrowStateClientRpc()
    {
        targetHand = null;
        if (rb != null)
        {
            rb.isKinematic = false;

        }
        if (col != null)
        {
            col.isTrigger = false;

        }
    }

      [ClientRpc]
    private void PerformThrowForceClientRpc(Vector3 dir, float force, ClientRpcParams rpcParams = default)
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(dir * force, ForceMode.Impulse);
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void ForceDropServerRpc()
    {
        if (!_isPicked.Value) return;
        _isPicked.Value = false;
        _heldByClientId.Value = ulong.MaxValue;
        PerformDropClientRpc();
    }


    [ClientRpc]
    public void ThrowFromGhostClientRpc(Vector3 force)
    {
        transform.SetParent(null);
        targetHand = null;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(force, ForceMode.Impulse);
        }
        if (col != null) col.isTrigger = false;
    }

    [ClientRpc]
    public void AttachToGhostHandClientRpc(ulong ghostNetworkObjectId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(ghostNetworkObjectId, out var ghostNetObj)) return;

        Transform ghostHand = ghostNetObj.transform.Find("Hand");
        if (ghostHand == null)
        {
            targetHand = ghostNetObj.transform;
        }
        else
        {
            targetHand = ghostHand;
        }

        if (rb != null) 
            rb.isKinematic = true;
        if (col != null)
            col.isTrigger = true;
    }
}