using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public NetworkVariable<PlayerRole> role = new NetworkVariable<PlayerRole>();
    public NetworkVariable<Vector3> netPos = new NetworkVariable<Vector3>();

    public bool IsGhost()
    {
        return role.Value == PlayerRole.Ghost;
    }

    public bool IsExorcist()
    {
        return role.Value == PlayerRole.Exorcist;
    }

    public override void OnNetworkSpawn()
    {

    }
}