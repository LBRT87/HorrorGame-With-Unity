using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GameExitHelper : MonoBehaviour
{
    public static GameExitHelper Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void GoToLobby() => StartCoroutine(ExitTo("Lobby"));
    public void GoToTitle() => StartCoroutine(ExitTo("Title"));

    private IEnumerator ExitTo(string targetPanel)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        MainMenuSceneController.TargetPanel = targetPanel;

        if (MultiPlayerManager.Instance?.CurrentGameMode == GameMode.Multiplayer)
            GameNetworkEvents.Instance?.BroadcastHostLeft();

        yield return new WaitForSeconds(0.3f);

        if (MultiPlayerManager.Instance?.CurrLobby != null)
            MultiPlayerManager.Instance.LeaveRoom();

        yield return new WaitForSeconds(0.5f); 

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            float timeout = 5f;
            while (NetworkManager.Singleton != null
                   && NetworkManager.Singleton.IsListening
                   && timeout > 0f)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }
        }

        yield return new WaitForSeconds(0.3f);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }
}