using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DisconnectPopupHandler : MonoBehaviour
{
    [Header("Popup Panel")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Buttons")]
    [SerializeField] private Button btnBackToLobby;
    [SerializeField] private Button btnBackToTitle;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenuScene";

    private static DisconnectPopupHandler _instance;
    public static DisconnectPopupHandler Instance => _instance;

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else { Destroy(gameObject); return; }

        if (popupPanel != null) popupPanel.SetActive(false);
    }

    private void Start()
    {
        if (btnBackToLobby != null)
            btnBackToLobby.onClick.AddListener(GoToLobby);

        if (btnBackToTitle != null)
            btnBackToTitle.onClick.AddListener(GoToTitle);

        if (MultiPlayerManager.Instance != null)
        {
            MultiPlayerManager.Instance.OnHostLeft += ShowHostLeftPopup;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDestroy()
    {
        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.OnHostLeft -= ShowHostLeftPopup;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }


    public void ShowHostLeftPopup()
    {
        ShowPopup(
            "Host Disconnected",
            "The host has left the game.\nYou will be returned to the menu."
        );
    }

    private void OnClientDisconnected(ulong clientId)
    {
        bool isLocalClient = clientId == NetworkManager.Singleton.LocalClientId;
        bool isServerShutdown = !NetworkManager.Singleton.IsConnectedClient
                                && !NetworkManager.Singleton.IsHost;

        if (isLocalClient || isServerShutdown)
        {
            ShowPopup(
                "Disconnected",
                "You have been disconnected from the server."
            );
        }
    }
    public void ShowPopup(string title, string message)
    {
        if (popupPanel == null) return;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        popupPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void GoToLobby()
    {
        MainMenuSceneController.TargetPanel = "Lobby";
        StartCoroutine(ShutdownAndLoad(mainMenuScene));
    }

    private void GoToTitle()
    {
        MainMenuSceneController.TargetPanel = "Title";
        StartCoroutine(ShutdownAndLoad(mainMenuScene));
    }

    private IEnumerator ShutdownAndLoad(string sceneName)
    {
        Time.timeScale = 1f;

        if (popupPanel != null) popupPanel.SetActive(false);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            yield return new WaitForSeconds(0.3f);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}