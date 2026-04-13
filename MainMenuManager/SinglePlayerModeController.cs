using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SinglePlayerMode : MonoBehaviour
{
    public UIManager nav;
    public LobbyUIController lobbyUI;

    public void KlikSinglePlayerMode()
    {
        MultiPlayerManager.Instance.CurrentGameMode = GameMode.SinglePlayer;
        Debug.Log("SinglePlayerMode");
        nav.ShowDynamicSwitchMenu(nav.singlePlayerMode);
    }

    public void klikButtonStart()
    {
        lobbyUI?.KlikStartSinglePlayer();
    }
}