using Unity.Netcode;
using UnityEngine;

public class MedkitItem : NetworkBehaviour
{
    [SerializeField] private int healAmount = 1; 

    public void UseOnTarget(HealthSystem target)
    {
        if (!IsOwner) return;
        UseOnTargetServerRpc(target.NetworkObjectId);
    }

    [ServerRpc(RequireOwnership =false)]
    private void UseOnTargetServerRpc(ulong targetNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
            .TryGetValue(targetNetworkId, out var netObj))
        {
            var health = netObj.GetComponent<HealthSystem>();
            if (health != null && !health.IsDead())
            {
                health.HealServerRpc(healAmount);
                GetComponent<NetworkObject>().Despawn();
            }
        }
    }
}