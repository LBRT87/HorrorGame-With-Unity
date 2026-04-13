using Unity.Netcode;
using UnityEngine;

public class GameResultManager : NetworkBehaviour
{
    public static GameResultManager Instance;

    private void Awake() => Instance = this;
    public void OnExorcistWin()
    {
        if (!IsServer) return;
        bool isMulti = MultiPlayerManager.Instance?.CurrentGameMode == GameMode.Multiplayer;
        NotifyResultClientRpc(true, isMulti);
    }
    public void OnGhostWin()
    {
        if (!IsServer) return;
        bool isMulti = MultiPlayerManager.Instance?.CurrentGameMode == GameMode.Multiplayer;
        NotifyResultClientRpc(false, isMulti);
    }

   [ClientRpc]
private void NotifyResultClientRpc(bool exorcistWin, bool isMultiplayer)
    {
        bool isGhost = false;
        foreach (var netObj in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
        {
            if (netObj.IsOwner)
            {
                isGhost = netObj.role.Value == PlayerRole.Ghost;
                break;
            }
        }

        bool localWin = isGhost ? !exorcistWin : exorcistWin;

        saveMAnager.Instance?.AddMatchResult(isMultiplayer, localWin);
        saveMAnager.Instance?.AddExpByResult(localWin, isMultiplayer);

        Debug.Log($"[GameResult] isGhost={isGhost} win={localWin} multi={isMultiplayer}");
    }
}