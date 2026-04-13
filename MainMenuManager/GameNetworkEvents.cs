using Unity.Netcode;
using UnityEngine;

public class GameNetworkEvents : NetworkBehaviour
{
    public static GameNetworkEvents Instance;

    private void Awake() => Instance = this;

    public void BroadcastHostLeft()
    {
        if (IsServer) HostLeftClientRpc();
    }

    [ClientRpc]
    private void HostLeftClientRpc()
    {
        if (IsHost) return;
        HUDManager.Instance?.GetExorcistHUD()?.ShowHostLeftPopup();
        HUDManager.Instance?.GetGhostHUD()?.ShowHostLeftPopup();
        Debug.Log("[GameNetworkEvents] Host left broadcast diterima");
    }
}