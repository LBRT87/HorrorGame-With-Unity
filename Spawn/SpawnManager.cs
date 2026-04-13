using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance;

    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject maleExorcistPrefab;
    [SerializeField] private GameObject femaleExorcistPrefab;

    [SerializeField] private GameObject kuntilPrefabs;
    [SerializeField] private GameObject weweGombelPrefabs;
    [SerializeField] private GameObject tuyulPrefabs;

    [Header("AI Ghost Prefabs")]
    [SerializeField] private GameObject kuntilAIPrefab;
    [SerializeField] private GameObject tuyulAIPrefab;
    [SerializeField] private GameObject weweAIPrefab;

    [SerializeField] private Transform[] aiGhostSpawnPoints;

    [SerializeField] private GameObject spiritBoxPrefab;
    [SerializeField] private GameObject notebookPrefab;
    [SerializeField] private GameObject ouijaBoardPrefab;

    private List<int> usedSpawnIndex = new List<int>();
    private HashSet<ulong> _spawnedClients = new HashSet<ulong>();

    private Dictionary<ulong, PlayerRole> _roleAssignment = new Dictionary<ulong, PlayerRole>();
    private Dictionary<ulong, GhostType> _ghostAssignment = new Dictionary<ulong, GhostType>();

    private int _totalGhostSlots = 1;
    private int _ghostsAssigned = 0;
    private List<GhostType> _availableGhostTypes = new List<GhostType>
        { GhostType.Kuntilanak, GhostType.Tuyul, GhostType.WeweGombel };

    private bool _setupStarted = false;
    private List<GhostType> _activeGhostTypes = new List<GhostType>();
    private List<int> usedGhostSpawnIndex = new List<int>();

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        StartCoroutine(DelaySpawn());
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        bool isSingle = MultiPlayerManager.Instance?.CurrentGameMode == GameMode.SinglePlayer;
        if (isSingle) return;

        if (_setupStarted && !_spawnedClients.Contains(clientId))
        {
            AssignRoleForClient(clientId);
            StartCoroutine(SpawnClientDelayed(clientId, 0.8f));
        }
    }

    private IEnumerator SpawnClientDelayed(ulong clientId, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_spawnedClients.Contains(clientId))
            SpawnOneClient(clientId);
    }

    private void AssignRoleForClient(ulong clientId, HashSet<GhostType> prefAssignedSet = null)
    {
        if (_roleAssignment.ContainsKey(clientId)) return;

        if (_ghostsAssigned < _totalGhostSlots && _availableGhostTypes.Count > 0)
        {
            string pref = MultiPlayerManager.Instance?.GetGhostPrefForClient(clientId) ?? "random";
            GhostType desired = GetGhostTypeFromPref(pref);

            bool prefAvailable = _availableGhostTypes.Contains(desired);
            bool prefNotTakenInBatch = prefAssignedSet == null || !prefAssignedSet.Contains(desired);

            if (!prefAvailable || !prefNotTakenInBatch)
            {
                GhostType fallback = GhostType.Kuntilanak;
                bool found = false;
                foreach (var gt in _availableGhostTypes)
                {
                    if (prefAssignedSet == null || !prefAssignedSet.Contains(gt))
                    {
                        fallback = gt;
                        found = true;
                        break;
                    }
                }
                desired = found ? fallback : _availableGhostTypes[Random.Range(0, _availableGhostTypes.Count)];
            }

            _ghostAssignment[clientId] = desired;
            _roleAssignment[clientId] = PlayerRole.Ghost;
            _availableGhostTypes.Remove(desired);
            _ghostsAssigned++;
            prefAssignedSet?.Add(desired);

            Debug.Log($"[SpawnManager] Assign GHOST {desired} ke client {clientId} (pref: {pref})");
        }
        else
        {
            _roleAssignment[clientId] = PlayerRole.Exorcist;
            Debug.Log($"[SpawnManager] Assign exorcist    client {clientId}");
        }
    }

    IEnumerator DelaySpawn()
    {
        yield return new WaitForSeconds(1f);

        bool isSingle = MultiPlayerManager.Instance?.CurrentGameMode == GameMode.SinglePlayer;

        if (OuijaSettings.IsActive)
        {
            var ouijaIntro = FindFirstObjectByType<OuijaGameIntro>();
            if (ouijaIntro != null)
            {
                bool introDone = false;
                ouijaIntro.PlayIntroIfActive(() => introDone = true);
                yield return new WaitUntil(() => introDone);
                Debug.Log("[SpawnManager] Ouija intro selesai");
            }
        }

        if (isSingle)
        {
            SpawnSinglePlayer();
            yield break;
        }

        _totalGhostSlots = MultiPlayerManager.Instance?.GetGhost() ?? 1;
        int expectedPlayers = MultiPlayerManager.Instance?.GetCurrPlayer() ?? 1;
        float waitTimeout = 25f;

        _availableGhostTypes = new List<GhostType>
            { GhostType.Kuntilanak, GhostType.Tuyul, GhostType.WeweGombel };

        _setupStarted = true;

        while (NetworkManager.Singleton.ConnectedClientsIds.Count < expectedPlayers && waitTimeout > 0)
        {
            yield return new WaitForSeconds(0.5f);
            waitTimeout -= 0.5f;
        }

        yield return new WaitForSeconds(1f);
        SpawnMultiPlayer();
    }

    private void SpawnSinglePlayer()
    {
        ulong hostId = NetworkManager.Singleton.LocalClientId;
        PlayerGender gender = PlayerGender.Female;
        if (MultiPlayerManager.Instance != null)
            gender = MultiPlayerManager.Instance.LocalPlayerGender;
        else if (saveMAnager.Instance != null)
            gender = saveMAnager.Instance.GetGenderPlayer() == "male" ? PlayerGender.Male : PlayerGender.Female;

        Debug.Log($"[SpawnManager] SinglePlayer gender: {gender}");

        SpawnNetworkPlayer(hostId,
            gender == PlayerGender.Male ? maleExorcistPrefab : femaleExorcistPrefab,
            PlayerRole.Exorcist);
        _spawnedClients.Add(hostId);

        if (HUDManager.Instance != null)
            StartCoroutine(DelaySpawnHUD(false));

        int ghostCount = Random.value < 0.70f ? 1 : 2;
        Debug.Log($"[SpawnManager] Single player: spawn {ghostCount} ghost AI");

        var pool = new List<GhostType> { GhostType.Kuntilanak, GhostType.Tuyul, GhostType.WeweGombel };
        Shuffle(pool);
        var active = new List<GhostType>();

        for (int i = 0; i < ghostCount && i < pool.Count; i++)
        {
            GhostType gt = pool[i];
            active.Add(gt);

            Transform pt = (aiGhostSpawnPoints != null && i < aiGhostSpawnPoints.Length)
                ? aiGhostSpawnPoints[i]
                : spawnPoints[Random.Range(0, spawnPoints.Length)];

            GameObject prefab = GetAIGhostPrefab(gt);
            if (prefab == null) { 
                Debug.LogError($"[SpawnManager] AI prefab null untuk"); 
                    continue; 
            }

            GameObject aiObj = Instantiate(prefab, pt.position, pt.rotation);
            var netObj = aiObj.GetComponent<NetworkObject>();
            if (netObj != null) 
                netObj.Spawn();
            else Debug.LogError($"[SpawnManager] AI prefab {prefab.name} gada NO");

            var aiBase = aiObj.GetComponent<GhostAIBase>();
            if (aiBase != null) aiBase.SetGhostType(gt);
        }

        SetupItemSpawner(active);

        SpawnStarterItemForClientRpc(GetStarterItemType(hostId), new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { hostId } }
        });
    }

    private IEnumerator DelaySpawnHUD(bool isGhost)
    {
        yield return null;
        yield return null;
        HUDManager.Instance?.SpawnHUDForRole(isGhost);
    }

    private void SpawnMultiPlayer()
    {
        var clients = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

        var batchAssigned = new HashSet<GhostType>();
        foreach (ulong id in clients)
            AssignRoleForClient(id, batchAssigned);

        _activeGhostTypes.Clear();
        foreach (ulong id in clients)
        {
            SpawnOneClient(id);
            if (_ghostAssignment.ContainsKey(id))
                _activeGhostTypes.Add(_ghostAssignment[id]);
        }

        SetupItemSpawner(_activeGhostTypes);
        Debug.Log($"[SpawnManager] Selesai. Ghosts: {string.Join(", ", _activeGhostTypes)}");

        foreach (ulong id in clients)
        {
            if (_roleAssignment.TryGetValue(id, out var role) && role == PlayerRole.Exorcist)
            {
                SpawnStarterItemForClientRpc(GetStarterItemType(id), new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { id } }
                });
            }
        }
    }

    [ClientRpc]
    private void SpawnStarterItemForClientRpc(StarterItemType itemType, ClientRpcParams rpcParams = default)
    {
        if (itemType == StarterItemType.None) { 
            Debug.Log("[SpawnManager] Tidak dapat starter item ");
            return;
        }
        StartCoroutine(AssignItemDelayed(itemType));
    }

private IEnumerator AssignItemDelayed(StarterItemType itemType)
{
    float timeout = 3f;
    StarterItemSystem sis = null;
    
    while (sis == null && timeout > 0)
    {
        yield return new WaitForSeconds(0.1f);
        timeout -= 0.1f;
        foreach (var s in FindObjectsByType<StarterItemSystem>(FindObjectsSortMode.None))
            if (s.IsOwner) { sis = s; break; }
    }
    
    if (sis == null) { Debug.LogError("[SpawnManager] StarterItemSystem timeout"); yield break; }
    sis.AssignStarterItemServerRpc(itemType);
}
    private void SpawnOneClient(ulong clientId)
    {
        if (_spawnedClients.Contains(clientId)) return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning($"[SpawnManager] Client {clientId} tidak connected!");
            return;
        }

        if (!_roleAssignment.ContainsKey(clientId))
            AssignRoleForClient(clientId);

        _spawnedClients.Add(clientId);

        PlayerRole role = _roleAssignment[clientId];

        if (role == PlayerRole.Ghost)
        {
            GhostType gt = _ghostAssignment.ContainsKey(clientId)
                ? _ghostAssignment[clientId]
                : GhostType.Kuntilanak;
            SpawnNetworkPlayer(clientId, GetNetworkGhostPrefab(gt), PlayerRole.Ghost);

            if (!_activeGhostTypes.Contains(gt))
                _activeGhostTypes.Add(gt);
        }
        else
        {
            PlayerGender gender = GetGenderForClient(clientId);
            GameObject prefab = gender == PlayerGender.Male ? maleExorcistPrefab : femaleExorcistPrefab;
            if (prefab == null) prefab = femaleExorcistPrefab;
            SpawnNetworkPlayer(clientId, prefab, PlayerRole.Exorcist);
        }
    }

    private void SpawnNetworkPlayer(ulong clientId, GameObject prefab, PlayerRole role)
    {
        if (prefab == null) { Debug.LogError($"[SpawnManager] Prefab NULL! client={clientId} role={role}"); return; }

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
        {
            Debug.LogWarning($"[SpawnManager] Client {clientId} sudah disconnect!");
            return;
        }

        Transform pt = role == PlayerRole.Ghost ? GetGhostSpawnPoint() : GetSpawnPoint();
        GameObject obj = Instantiate(prefab, pt.position, pt.rotation);
        var netObj = obj.GetComponent<NetworkObject>();

        if (netObj == null) { Debug.LogError($"[SpawnManager] {prefab.name} tidak punya NetworkObject!"); Destroy(obj); return; }

        netObj.SpawnAsPlayerObject(clientId, true);

        var pn = obj.GetComponent<PlayerNetwork>();
        if (pn != null) pn.role.Value = role;

        Debug.Log($"[SpawnManager] Spawn {role} prefab={prefab.name} client={clientId}");
        StartCoroutine(DelayedHUDNotify(clientId, role));
    }

    private IEnumerator DelayedHUDNotify(ulong clientId, PlayerRole role)
    {
        yield return null;
        NotifyHUDClientRpc(role, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    private void NotifyHUDClientRpc(PlayerRole role, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[SpawnManager] Client HUD setup: {role}");
        bool isGhost = role == PlayerRole.Ghost;
        HUDManager.Instance?.SpawnHUDForRole(isGhost);
    }

    private StarterItemType GetStarterItemType(ulong clientId)
    {
        if (MultiPlayerManager.Instance?.CurrLobby != null)
        {
            var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            int idx = ids.IndexOf(clientId);
            if (idx >= 0 && idx < MultiPlayerManager.Instance.CurrLobby.Players.Count)
            {
                var p = MultiPlayerManager.Instance.CurrLobby.Players[idx];
                if (p.Data != null && p.Data.ContainsKey("StarterItem"))
                    return ParseStarterItem(p.Data["StarterItem"].Value);
            }
        }
        return StarterItemType.None;
    }

    private StarterItemType ParseStarterItem(string s) => s?.ToLower() switch
    {
        "spiritbox" => StarterItemType.SpiritBox,
        "notebook" => StarterItemType.Notebook,
        "ouijaboard" => StarterItemType.OuijaBoard,
        _ => StarterItemType.None
    };

    public Transform GetGhostSpawnPoint()
    {
        if (aiGhostSpawnPoints == null || aiGhostSpawnPoints.Length == 0)
            return GetSpawnPoint();

        for (int i = 0; i < aiGhostSpawnPoints.Length; i++)
        {
            if (!usedGhostSpawnIndex.Contains(i))
            {
                usedGhostSpawnIndex.Add(i);
                return aiGhostSpawnPoints[i];
            }
        }
        return aiGhostSpawnPoints[Random.Range(0, aiGhostSpawnPoints.Length)];
    }

    private PlayerGender GetGenderForClient(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId && saveMAnager.Instance != null)
            return saveMAnager.Instance.GetGenderPlayer() == "male" ? PlayerGender.Male : PlayerGender.Female;

        if (MultiPlayerManager.Instance?.CurrLobby != null)
        {
            var ids = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
            int idx = ids.IndexOf(clientId);
            if (idx >= 0 && idx < MultiPlayerManager.Instance.CurrLobby.Players.Count)
            {
                var p = MultiPlayerManager.Instance.CurrLobby.Players[idx];
                if (p.Data != null && p.Data.ContainsKey("Gender"))
                    return p.Data["Gender"].Value == "Male" ? PlayerGender.Male : PlayerGender.Female;
            }
        }
        return PlayerGender.Female;
    }

    private void SetupItemSpawner(List<GhostType> active)
    {
        if (GameRecipeRegistry.Instance != null)
            GameRecipeRegistry.Instance.GenerateRecipes(active.ToArray());
        if (ItemSpawner.Instance != null)
            ItemSpawner.Instance.SpawnAllItems();
        if (PaperSpawner.Instance != null)
            PaperSpawner.Instance.SpawnPapers();
    }

    private GhostType GetGhostTypeFromPref(string pref) => pref switch
    {
        "kunti" => GhostType.Kuntilanak,
        "tuyul" => GhostType.Tuyul,
        "wewe" => GhostType.WeweGombel,
        _ => (GhostType)Random.Range(0, 3)
    };

    private GameObject GetAIGhostPrefab(GhostType gt) => gt switch
    {
        GhostType.Kuntilanak => kuntilAIPrefab,
        GhostType.Tuyul => tuyulAIPrefab,
        _ => weweAIPrefab
    };

    private GameObject GetNetworkGhostPrefab(GhostType gt) => gt switch
    {
        GhostType.Kuntilanak => kuntilPrefabs,
        GhostType.Tuyul => tuyulPrefabs,
        _ => weweGombelPrefabs
    };

    public Transform GetSpawnPoint()
    {
        for (int i = 0; i < spawnPoints.Length; i++)
            if (!usedSpawnIndex.Contains(i)) { usedSpawnIndex.Add(i); return spawnPoints[i]; }
        return spawnPoints[Random.Range(0, spawnPoints.Length)];
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}

public enum StarterItemType { None, SpiritBox, Notebook, OuijaBoard }