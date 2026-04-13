using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkSessionManager : NetworkBehaviour
{
    public static NetworkSessionManager Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "Lobby";

    private NetworkVariable<int> _ghostType0 = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> _ghostType1 = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool _isCleaningUp = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
    }


    [ServerRpc(RequireOwnership = false)]
    public void SetGhostReferenceServerRpc(int ghostTypeInt, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        int idx = 0;
        foreach (var id in connectedClients)
        {
            if (id == clientId) break;
            idx++;
        }

        if (idx == 0) _ghostType0.Value = ghostTypeInt;
        else _ghostType1.Value = ghostTypeInt;

        Debug.Log($"[SessionManager] Ghost reference set: client {clientId} → {(GhostType)ghostTypeInt}");
    }

    public GhostType GetGhostTypeForClient(ulong clientId)
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        int idx = 0;
        foreach (var id in connectedClients)
        {
            if (id == clientId) break;
            idx++;
        }

        int typeInt = (idx == 0) ? _ghostType0.Value : _ghostType1.Value;
        return (GhostType)typeInt;
    }


    private void OnClientDisconnect(ulong clientId)
    {
        bool isGhost = IsGhostPlayer(clientId);

        if (isGhost || clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowDisconnectPopup();
            Debug.Log($"[SessionManager] Disconnect: client {clientId} (isGhost={isGhost})");
        }
    }

    private bool IsGhostPlayer(ulong clientId)
    {
        foreach (var pn in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
        {
            if (pn.OwnerClientId == clientId && pn.role.Value == PlayerRole.Ghost)
                return true;
        }
        return false;
    }

    private void ShowDisconnectPopup()
    {
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null)
            hud.OpenDisconnectPanel();
        else
            Debug.LogWarning("[SessionManager] HUD null saat disconnect");
    }

    private void HideDisconnectPopup()
    {
        var hud = FindFirstObjectByType<HUD>();
        hud?.CloseDisconnectPanel();
    }
    public void OnDisconnectPanelBackToLobby()
    {
        BackToLobby();
    }

    public void OnDisconnectPanelClose()
    {
        HideDisconnectPopup();
    }

    public void BackToLobby()
    {
        if (_isCleaningUp) return;
        _isCleaningUp = true;
        StartCoroutine(BackToLobbyRoutine());
    }

    private IEnumerator BackToLobbyRoutine()
    {
        HideDisconnectPopup();

        Debug.Log("[SessionManager] Back to lobby: cleanup...");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();

            float timeout = 3f;
            while (NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);
        Debug.Log("[SessionManager] Loading lobby...");
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);

        _isCleaningUp = false;
    }
}