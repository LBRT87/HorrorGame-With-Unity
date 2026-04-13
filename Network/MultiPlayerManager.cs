using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiPlayerManager : MonoBehaviour{
    public static MultiPlayerManager Instance;
    private int maxPlayer = 5;

    public System.Action OnRoomCreatedDetail;
    public System.Action OnRoomJoin;
    public System.Action<List<string>> ListPlayer;
    public System.Action<int> GhostCounts;
    public System.Action GameStart;

    public Lobby CurrLobby ;
    private string LocalPlayerName;
    private float LobbyHeartBeatTimer;
    private float LobbyPollTimer = 6f;
    private bool _isPolling = false;
    private bool isJoiningGame = false;

    public PlayerGender LocalPlayerGender = PlayerGender.Female;
    public GameMode CurrentGameMode = GameMode.SinglePlayer;

    private bool isReady = false;
    public System.Action OnKicked;       
    public System.Action OnHostLeft;

    public System.Action OnReady;
    public bool IsServiceReady => isReady;

    private readonly Dictionary<ulong, string> _clientToPlayerId = new();
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
        }else
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);  
    }

    async void Start()
    {
        var options = new InitializationOptions();

        #if UNITY_EDITOR
        string uniqueId = UnityEditor.EditorPrefs.GetString("MultiplayerTestProfile", "");
        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = "Editor_" + System.Guid.NewGuid().ToString()[..8];
            UnityEditor.EditorPrefs.SetString("MultiplayerTestProfile", uniqueId);
        }
        options.SetProfile(uniqueId);
        Debug.Log("[Auth] Profile: " + uniqueId);
        #else
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            options.SetProfile("Build_" + deviceId[..8]);
            Debug.Log("[Auth] Build Profile: " + "Build_" + deviceId[..8]);
        #endif

        await UnityServices.InitializeAsync(options);

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        isReady = true;
        Debug.Log("Ready Join");
        OnReady?.Invoke();
        Debug.Log("Authentikasi ID: " + AuthenticationService.Instance.PlayerId);
    }

    private bool IsHost()
    {
        bool ishost = CurrLobby != null && CurrLobby.HostId == AuthenticationService.Instance.PlayerId;
        return ishost;
    }


    public void RegisterClientPlayerId(ulong clientId, string playerId)
    {
        _clientToPlayerId[clientId] = playerId;
        Debug.Log($"[MultiPlayerManager] Register client {clientId}  player {playerId}");
    }

    private void LobbyHeartBeatManage ()
    {
        if (CurrLobby == null || !IsHost())
        {
            return ;
        }
        LobbyHeartBeatTimer -= Time.deltaTime;

        if (LobbyHeartBeatTimer <= 0f)
        {
            LobbyHeartBeatTimer = 15f;
            LobbyService.Instance.SendHeartbeatPingAsync(CurrLobby.Id);
        }
    }

    private void UpdateListPlayers()
    {
        if (CurrLobby == null) return;
        var names = new List<string>();
        foreach (var i in CurrLobby.Players)
        {
            string nama = i.Data.ContainsKey("UsernamePlayer")
                ? i.Data["UsernamePlayer"].Value : "Aleen";
            names.Add(nama);
        }
        ListPlayer?.Invoke(names);
    }


    public async void CreateRoom(string usernamePlayer, string roomName, int ghostCount = 1, bool isPrivate = false)
    {
        if (!isReady)
        {
            Debug.Log("Service lom ready");
            return;
        }
        LocalPlayerName = usernamePlayer;
        try
        {
        string ghostPrefKey = "GhostPref_" + AuthenticationService.Instance.PlayerId;
        string ghostPrefVal = saveMAnager.Instance != null ? saveMAnager.Instance.GetGhostPref() : "random"; 

        var lobiesOption = new CreateLobbyOptions
        {
            IsPrivate = isPrivate,
            Data = new Dictionary<string, DataObject>
            {
                { "RelayRoomCode", new DataObject(DataObject.VisibilityOptions.Member, "") },
                { "AmountGhost",   new DataObject(DataObject.VisibilityOptions.Public, ghostCount.ToString()) },
                { "KickedPlayer",  new DataObject(DataObject.VisibilityOptions.Member, "") },
                { "HostLeft",      new DataObject(DataObject.VisibilityOptions.Member, "false") },
                { ghostPrefKey,    new DataObject(DataObject.VisibilityOptions.Member, ghostPrefVal) },
                { "GameStarted", new DataObject(DataObject.VisibilityOptions.Public,"false", DataObject.IndexOptions.S1) },
            },
                        Player = MakeDataPlayer(usernamePlayer)
            };
            CurrLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, maxPlayer, lobiesOption);
            OnRoomCreatedDetail?.Invoke();
            UpdateListPlayers();
            Debug.Log("LOBBY CODE:" + CurrLobby.LobbyCode);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal buat room: " + e.Message);
            Debug.LogError("Stack: " + e.StackTrace); 
            Debug.LogError("Source: " + e.Source);    
        }
    }

    private void SetTransHost(Allocation alokasi)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(alokasi.RelayServer.IpV4,(ushort) alokasi.RelayServer.Port, alokasi.AllocationIdBytes, alokasi.Key, alokasi.ConnectionData,false);
    }

    private void SetTransClient(JoinAllocation joinalok)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(joinalok.RelayServer.IpV4, (ushort)joinalok.RelayServer.Port, joinalok.AllocationIdBytes, joinalok.Key, joinalok.ConnectionData, joinalok.HostConnectionData, false);
    }

    private Player MakeDataPlayer(string usernamePlayer)
    {
        string pref = saveMAnager.Instance != null ? saveMAnager.Instance.GetGhostPref() : "random"; 

        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
        {
            { "UsernamePlayer", new PlayerDataObject(
                PlayerDataObject.VisibilityOptions.Member, usernamePlayer) },
            { "Gender", new PlayerDataObject(
                PlayerDataObject.VisibilityOptions.Member, LocalPlayerGender.ToString()) },
            { "GhostPref", new PlayerDataObject(
                PlayerDataObject.VisibilityOptions.Member, pref) }
        }
        };
    }

    public async void JoinRoom(string usernamePlayer,string roomCode)
    {
        if (!isReady)
        {
            Debug.LogError("Belum ready");
            return;
        }
        LocalPlayerName = usernamePlayer;
        try
        {
            var joinRoomOPtion = new JoinLobbyByCodeOptions
            {
                Player = MakeDataPlayer(usernamePlayer)
            };
            CurrLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(roomCode,joinRoomOPtion);
            await UploadMyGhostPref();
            OnRoomJoin?.Invoke();
            UpdateListPlayers();
            Debug.Log("Barusan join " + roomCode);
        }catch(System.Exception even)
        {
            Debug.LogError("Gagal buat join room , errony " + even.Message);
        } 
    }



  public async void LeaveRoom()
    {
        isJoiningGame = false;

        try
        {
            if (CurrLobby != null)
            {
                if (IsHost())
                {
                    await LobbyService.Instance.UpdateLobbyAsync(CurrLobby.Id, new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                    {
                        { "HostLeft",    new DataObject(DataObject.VisibilityOptions.Member, "true") },
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }, 
                        { "RelayRoomCode", new DataObject(DataObject.VisibilityOptions.Member, "") }   
                    }
                    });
                    await System.Threading.Tasks.Task.Delay(500);
                    await LobbyService.Instance.DeleteLobbyAsync(CurrLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(CurrLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal keluar room: " + e.Message);
        }
        finally
        {
            CurrLobby = null;
            isJoiningGame = false;
            _clientToPlayerId.Clear();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            Debug.Log("[MultiPlayerManager] LeaveRoom selesai, state di-reset");
        }
    }

    public async Task StartGame()
    {
        if (!IsHost()) return;

        int totalPlayer = CurrLobby.Players.Count;
        int totalGhost = int.Parse(CurrLobby.Data["AmountGhost"].Value);
        Debug.Log("Total Ghost : " + totalGhost);

        int minimumPlayer = totalGhost == 2 ? 4 : 2;

        if (totalPlayer < minimumPlayer)
        {
            Debug.LogWarning("Minimum Players " + minimumPlayer + " jika ghost " + totalGhost);
            return;
        }

        Allocation alokasi = await RelayService.Instance.CreateAllocationAsync(5);
        string relayCode = await RelayService.Instance.GetJoinCodeAsync(alokasi.AllocationId);

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(CurrLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                {"RelayRoomCode", new DataObject(DataObject.VisibilityOptions.Member, relayCode)},
                {"GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "true")}
            }
            });

            SetTransHost(alokasi);
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);

            Debug.Log("Host aman, relay = " + relayCode);
        }
        catch (System.Exception even)
        {
            Debug.LogError("Gagal mulai: " + even.Message);
        }
    }

    private async Task CekRuleGameStart()
    {


        if (!IsHost() && !isJoiningGame &&
            CurrLobby.Data.ContainsKey("GameStarted") &&
            CurrLobby.Data["GameStarted"].Value == "true")
        {
            string relayCodes = CurrLobby.Data.ContainsKey("RelayRoomCode")? CurrLobby.Data["RelayRoomCode"].Value : "";

            if (string.IsNullOrEmpty(relayCodes))
            {
                Debug.LogWarning("[MultiPlayerManager] GameStarted=true tapi relay kosong, skip join");
                return;
            }

            Debug.Log("Mulai buat relay");
            isJoiningGame = true;
            try
            {
                string relayCode = CurrLobby.Data["RelayRoomCode"].Value;
                Debug.Log("Ini relay " + relayCode);
                
                JoinAllocation joinalok = await RelayService.Instance.JoinAllocationAsync(relayCode);
                SetTransClient(joinalok);
                await Task.Delay(2500);

                NetworkManager.Singleton.StartClient();

                float timeout = 15f;
                while (!NetworkManager.Singleton.IsConnectedClient && timeout > 0)
                {
                    await Task.Delay(200);
                    timeout -= 0.2f;
                }

                if (!NetworkManager.Singleton.IsConnectedClient)
                {
                    Debug.LogError("Clien gagal konek dalam 10 second");
                    isJoiningGame = false;
                    return;
                }

                Debug.Log("Client berhasil Join");
            }
            catch (System.Exception e)
            {
                isJoiningGame = false;
                Debug.LogError("Client gagal join game: " + e.Message);
            }
            return;
        }

        if (IsHost())
        {
            int ghost = int.Parse(CurrLobby.Data["AmountGhost"].Value);
            int minimumplayer = ghost == 2 ? 4 : 2;
            if (CurrLobby.Players.Count >= minimumplayer)
            {
                GameStart?.Invoke();
            }
        }
    }


    public int GetCurrPlayer()
    {
        if (CurrLobby != null)
        {
            return CurrLobby.Players.Count;
        }else
        {
            return 0;
        }
    }
    
    public int GetGhost() => CurrLobby != null ? int.Parse(CurrLobby.Data["AmountGhost"].Value) : 0;

    public bool GetHost() => IsHost();

    void Update()
    {
        LobbyHeartBeatManage();
        if (!isReady)return;
        LobbyPollingManagePlayerListUpdate();
    }

    private async void OnApplicationQuit()
    {
        if (CurrLobby != null)
        {
            try
            {
                if (IsHost())
                    await LobbyService.Instance.DeleteLobbyAsync(CurrLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(
                        CurrLobby.Id,
                        AuthenticationService.Instance.PlayerId);
            }
            catch { }
        }
    }

    public void KlikMultiplayerMode()
    {
        MultiPlayerManager.Instance.CurrentGameMode = GameMode.Multiplayer;
        Debug.Log("Sekarang Multiplayermode");
    }
    public async Task UpdateGhostAmount(int amount)
    {
        if (!IsHost() || CurrLobby == null) return;
        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(CurrLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                { "AmountGhost", new DataObject(DataObject.VisibilityOptions.Public, amount.ToString()) }
            }
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal update ghost amount: " + e.Message);
        }
    }


    public async Task<List<Lobby>> GetPublicRooms()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                new QueryFilter(QueryFilter.FieldOptions.S1, "false", QueryFilter.OpOptions.EQ)
            },
                Order = new List<QueryOrder>
            {
                new QueryOrder(false, QueryOrder.FieldOptions.Created)
            }
            };
            var result = await LobbyService.Instance.QueryLobbiesAsync(options);
            return result.Results.FindAll(e => !e.IsPrivate);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal fetch public rooms: " + e.Message);
            return new List<Lobby>();
        }
    }


    public async void JoinRoomById(string usernamePlayer, string lobbyId)
    {
        if (!isReady) return;
        LocalPlayerName = usernamePlayer;
        try
        {
            var options = new JoinLobbyByIdOptions { Player = MakeDataPlayer(usernamePlayer) };
            CurrLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            await UploadMyGhostPref();
            OnRoomJoin?.Invoke();
            UpdateListPlayers();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal join by id: " + e.Message);
        }
    }

    public async void SetRoomPrivate(bool isPrivate)
    {
        if (!IsHost() || CurrLobby == null) return;
        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(CurrLobby.Id, new UpdateLobbyOptions
            {
                IsPrivate = isPrivate
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal set private: " + e.Message);
        }
    }
    public async void KickPlayer(string playerId)
    {
        if (!IsHost() || CurrLobby == null) return;
        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(CurrLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                { "KickedPlayer", new DataObject(DataObject.VisibilityOptions.Member, playerId) }
            }
            });
            await System.Threading.Tasks.Task.Delay(2500);
            await LobbyService.Instance.RemovePlayerAsync(CurrLobby.Id, playerId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal kick: " + e.Message);
        }
    }
    public System.Action<List<Lobby>> OnPublicRoomsRefreshed;

    public async void RefreshPublicRooms()
    {
        Debug.Log("[Lobby] Refreshing public rooms...");
        var rooms = await GetPublicRooms();
        Debug.Log($"[Lobby] Ditemukan {rooms.Count} room");
        OnPublicRoomsRefreshed?.Invoke(rooms);
    }
    public async Task UploadMyGhostPref()
    {
        if (CurrLobby == null) return;
        string pref = saveMAnager.Instance?.GetGhostPref() ?? "random";
        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(
                CurrLobby.Id,
                AuthenticationService.Instance.PlayerId,
                new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                    { "GhostPref", new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member, pref) }
                    }
                });
            Debug.Log("[Lobby] Ghost pref uploaded: " + pref);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Gagal upload ghost pref: " + e.Message);
        }
    }
    private async void LobbyPollingManagePlayerListUpdate()
    {
        if (CurrLobby == null || isJoiningGame) return;
        if (_isPolling) return; 

        LobbyPollTimer -= Time.deltaTime;
        if (LobbyPollTimer > 0f) return;
        LobbyPollTimer = 6f; 

        _isPolling = true;
        try
        {
            var lobby = await LobbyService.Instance.GetLobbyAsync(CurrLobby.Id);
            CurrLobby = lobby;

            if (!IsHost() &&
                lobby.Data.ContainsKey("HostLeft") &&
                lobby.Data["HostLeft"].Value == "true")
            {
                CurrLobby = null;
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.Shutdown();
                OnHostLeft?.Invoke();
                return;
            }

            if (!IsHost() &&
                lobby.Data.ContainsKey("KickedPlayer") &&
                lobby.Data["KickedPlayer"].Value == AuthenticationService.Instance.PlayerId)
            {
                CurrLobby = null;
                if (NetworkManager.Singleton != null)
                    NetworkManager.Singleton.Shutdown();
                OnKicked?.Invoke();
                return;
            }

            UpdateListPlayers();
            await CekRuleGameStart();
        }
        catch (LobbyServiceException e)
        {
            if (e.Reason == LobbyExceptionReason.RateLimited)
            {
                Debug.LogWarning("[Lobby] Rate limited, tunggu 10 detik...");
                LobbyPollTimer = 10f; 
            }
            else
            {
                Debug.LogError("Error di polling: " + e.Message);
                CurrLobby = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error di polling: " + e.Message);
            Debug.LogError("Stack polling: " + e.StackTrace); 
        }
        finally
        {
            _isPolling = false; 
        }
    }
    public string GetGhostPrefForClient(ulong clientId)
    {
        if (CurrLobby == null) return "random";

        if (_clientToPlayerId.TryGetValue(clientId, out string playerId))
        {
            foreach (var player in CurrLobby.Players)
            {
                if (player.Id == playerId)
                {
                    string pref = player.Data != null && player.Data.ContainsKey("GhostPref")
                        ? player.Data["GhostPref"].Value
                        : "random";
                    Debug.Log($"[MultiPlayerManager] Ghost pref untuk client {clientId}: {pref}");
                    return pref;
                }
            }
        }

        var connectedIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        int idx = connectedIds.IndexOf(clientId);
        if (idx < 0 || idx >= CurrLobby.Players.Count) return "random";
        var p = CurrLobby.Players[idx];
        string result = p.Data != null && p.Data.ContainsKey("GhostPref")
            ? p.Data["GhostPref"].Value
            : "random";
        return result;
    }
    public async void UploadMyGhostPrefPublic()
{
    await UploadMyGhostPref(); 
}

    public async void UploadUsernameToLobby(string username)
    {
        var lobby = CurrLobby;
        if (lobby == null) return;

        await LobbyService.Instance.UpdatePlayerAsync(
            lobby.Id,
            AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                {
                    "UsernamePlayer",
                    new PlayerDataObject(
                        PlayerDataObject.VisibilityOptions.Member,
                        username)
                }
                }
            }
        );
    }
}
