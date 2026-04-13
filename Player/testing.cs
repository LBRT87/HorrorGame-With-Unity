using Unity.Netcode;
using UnityEngine;

public class TestLobby : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] ghostPrefabs;
    public GameObject exorcistPrefab;

    [Header("Spawn Points")]
    public Transform ghostSpawnPoint;
    public Transform exorcistSpawnPoint;

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 220, 300));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
            if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
        }
        else
        {
            string mode = NetworkManager.Singleton.IsHost ? "Host" : "Client";
            GUILayout.Label($"Mode: {mode}");
            GUILayout.Label($"Players: {NetworkManager.Singleton.ConnectedClients.Count}");

            bool isConnected = NetworkManager.Singleton.IsConnectedClient;

            GUILayout.Space(5);
            GUILayout.Label("── Ghost ──");
            if (isConnected && GUILayout.Button("Spawn Kuntilanak")) SpawnGhostServerRpc(0);
            if (isConnected && GUILayout.Button("Spawn Tuyul")) SpawnGhostServerRpc(1);
            if (isConnected && GUILayout.Button("Spawn Wewe Gombel")) SpawnGhostServerRpc(2);

            GUILayout.Space(5);
            GUILayout.Label("── Exorcist ──");
            if (isConnected && GUILayout.Button("Spawn Exorcist")) SpawnExorcistServerRpc();
        }

        GUILayout.EndArea();
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnGhostServerRpc(int ghostType, ServerRpcParams rpc = default)
    {
        ulong clientId = rpc.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.LogWarning($"[TestLobby] Client {clientId} sudah punya player!");
                return;
            }
        }

        if (ghostPrefabs == null || ghostType >= ghostPrefabs.Length || ghostPrefabs[ghostType] == null)
        {
            Debug.LogError($"[TestLobby] ghostPrefabs[{ghostType}] null/missing!");
            return;
        }

        var go = Instantiate(ghostPrefabs[ghostType], ghostSpawnPoint.position, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void SpawnExorcistServerRpc(ServerRpcParams rpc = default)
    {
        ulong clientId = rpc.Receive.SenderClientId;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                Debug.LogWarning($"[TestLobby] Client {clientId} sudah punya player!");
                return;
            }
        }

        if (exorcistPrefab == null)
        {
            Debug.LogError("[TestLobby] exorcistPrefab null!");
            return;
        }

        var go = Instantiate(exorcistPrefab, exorcistSpawnPoint.position, Quaternion.identity);
        go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }
}