using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUIController : MonoBehaviour
{
    public UIManager nav;
    public TMP_InputField createRoomNameInput;
    public TMP_InputField joinRoomCodeInput;
    public TMP_InputField usernameInput;

    public TextMeshProUGUI roomCodeTextPovHost;
    public TextMeshProUGUI roomNameTextPovHost;
    public TextMeshProUGUI roomCodeTextPovClient;
    public TextMeshProUGUI roomNameTextPovClient;
    public Button startButton;
    public Button leaveButton;

    public Transform playerListContentClient;
    public Transform playerListContentHost;
    public GameObject playerNamePrefab;
    public Button btnFemale;
    public Button btnMale;

    public Button btnRandom;
    public Button btnKunti;
    public Button btnTuyul;
    public Button btnWewe;
    public Button btnGhost1;
    public Button btnGhost2;
    public GameObject ghostAmountPanel;

    public GameObject popupPanel;
    public Button btnPopupClose;

    private string selectedGender = "female";
    private string selectedGhostPref = "random";
    private int selectedGhostAmount = 1;
    public Transform roomListContent;     
    public GameObject roomRowPrefab;        
    public Button btnRefreshRooms;

    public Toggle togglePrivate;            

    public GameObject popupKickedPanel;
    public Button btnPopupKickedClose;
    public GameObject popupHostLeftPanel;
    public Button btnPopupHostLeftClose;

    public TextMeshProUGUI teksGender;
    public TextMeshProUGUI teksGhost;
    public TextMeshProUGUI teksItem;
    public Button btnspiritbox;
    public Button btnnotebook;

    public Button btnOuija;
    public TextMeshProUGUI teksOuija;

    public TextMeshProUGUI matchSingleText;
    public TextMeshProUGUI matchMultiText;
    public TextMeshProUGUI winSingleText;
    public TextMeshProUGUI winMultiText;
    public TextMeshProUGUI teksLevel;
    public TextMeshProUGUI teksUsername;

    public GameObject popupLevelLockPanel;
    public Button btnPopupLevelLockClose;
    public TextMeshProUGUI teksPopupLevelLock;
    private void Start()
    {
        MultiPlayerManager.Instance.OnRoomCreatedDetail += UpdateLobbyUIHost;
        MultiPlayerManager.Instance.ListPlayer += UpdatePlayerList;
        MultiPlayerManager.Instance.GameStart += EnableStartButton;
        MultiPlayerManager.Instance.OnRoomJoin += UpdateLobbyUIClient;
        MultiPlayerManager.Instance.OnKicked += OnKickedFromRoom;
        MultiPlayerManager.Instance.OnHostLeft += OnHostLeftRoom;

        btnRefreshRooms.onClick.AddListener(RefreshPublicRooms);
        btnPopupKickedClose.onClick.AddListener(() => popupKickedPanel.SetActive(false));
        btnPopupHostLeftClose.onClick.AddListener(() => popupHostLeftPanel.SetActive(false));
        popupLevelLockPanel.SetActive(false);
        btnPopupLevelLockClose.onClick.AddListener(() => popupLevelLockPanel.SetActive(false));
        popupKickedPanel.SetActive(false);
        popupHostLeftPanel.SetActive(false);

        RefreshAllFromSave();
        SyncGenderToSession(selectedGender);
        RefreshGhostAmountUI();

        btnFemale.onClick.AddListener(() => SelectGender("female"));
        btnMale.onClick.AddListener(() => SelectGender("male"));
        btnRandom.onClick.AddListener(() => SelectGhostPref("random"));
        btnKunti.onClick.AddListener(() => SelectGhostPref("kunti"));
        btnTuyul.onClick.AddListener(() => SelectGhostPref("tuyul"));
        btnWewe.onClick.AddListener(() => SelectGhostPref("wewe"));
        btnspiritbox.onClick.AddListener(() => SelectItem("spiritbox"));
        btnnotebook.onClick.AddListener(() => SelectItem("notebook"));
        btnOuija?.onClick.AddListener(ToggleOuija);
        RefreshOuijaUI();
        btnGhost1.onClick.AddListener(() => SelectGhostAmount(1));
        btnGhost2.onClick.AddListener(() => SelectGhostAmount(2));
        btnPopupClose.onClick.AddListener(() => popupPanel.SetActive(false));
        popupPanel.SetActive(false);

        if (ghostAmountPanel != null)
        {
            ghostAmountPanel.SetActive(false);
        }

        if (MultiPlayerManager.Instance.IsServiceReady)
        {
            RefreshPublicRooms();
        }else
        {
            MultiPlayerManager.Instance.OnReady += RefreshPublicRooms;
        }


    }

    private bool CheckLevelRequirement(int requiredLevel, string featureName)
    {
        int currLevel = saveMAnager.Instance?.GetLevel() ?? 1;
        if (currLevel >= requiredLevel) return true;

        if (teksPopupLevelLock != null)
            teksPopupLevelLock.text = $"{featureName} unlocks at level {requiredLevel}.\nYour level: {currLevel}";

        popupLevelLockPanel.SetActive(true);
        return false;
    }
    private void UpdateLobbyUIHost()
    {
        if (playerListContentHost == null)
        {
            Debug.LogError("[LobbyUI] playerListContent Host blm di assign di Inspector!");
            return;
        }
        if (playerListContentClient == null)
        {
            Debug.LogError("[LobbyUI] playerListContent Client blm di assinhg di Inspector!");
            return;
        }
        if (playerNamePrefab == null)
        {
            Debug.LogError("[LobbyUI] playerNamePrefab bml di assing di Inspector");
            return;
        }
        if (MultiPlayerManager.Instance.CurrLobby == null) return;
        roomCodeTextPovHost.text = MultiPlayerManager.Instance.CurrLobby.LobbyCode;
        roomNameTextPovHost.text = MultiPlayerManager.Instance.CurrLobby.Name;

        if (ghostAmountPanel != null)
            ghostAmountPanel.SetActive(true);

        var names = new List<string>();
        foreach (var p in MultiPlayerManager.Instance.CurrLobby.Players)
        {
            string nama = p.Data.ContainsKey("UsernamePlayer")
                ? p.Data["UsernamePlayer"].Value : "Aleen";
            names.Add(nama);
        }
        UpdatePlayerList(names);

        nav.ShowDynamicSwitchMenu(nav.lobbyHost);
    }
    private void OnEnable()
    {
        RefreshAllFromSave();
    }
    private async void RefreshPublicRooms()
    {
        if (btnRefreshRooms != null) btnRefreshRooms.interactable = false;

        foreach (Transform child in roomListContent)
            Destroy(child.gameObject);

        if (roomRowPrefab == null)
        {
            Debug.LogError("[LobbyUI] roomRowPrefab belum diassign");
            if (btnRefreshRooms != null) btnRefreshRooms.interactable = true;
            return;
        }

        var rooms = await MultiPlayerManager.Instance.GetPublicRooms();
        Debug.Log($"[LobbyUI] Public rooms: {rooms.Count}");

        if (roomListContent == null)
        {
            if (btnRefreshRooms != null) btnRefreshRooms.interactable = true;
            return;
        }

        if (rooms.Count == 0)
        {
            var empty = new GameObject("EmptyText");
            empty.transform.SetParent(roomListContent, false);
            var txt = empty.AddComponent<TextMeshProUGUI>();
            txt.text = "Tidak ada room tersedia";
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 14;
        }

        foreach (var room in rooms)
        {
            if (roomRowPrefab == null || roomListContent == null) break;

            GameObject row = Instantiate(roomRowPrefab, roomListContent, false);
            var namaText = row.transform.Find("roomRow/namaroom")?.GetComponent<TextMeshProUGUI>();
            var playerText = row.transform.Find("roomRow/totalppayer")?.GetComponent<TextMeshProUGUI>();
            Debug.Log($"[LobbyUI] namaText={namaText}, playerText={playerText}, roomName={room.Name}");

            if (namaText != null) namaText.text = room.Name;
            if (playerText != null) playerText.text = $"{room.Players.Count}/{room.MaxPlayers}";
            var btn = row.GetComponent<Button>();
            if (btn != null)
            {
                bool isFull = room.Players.Count >= room.MaxPlayers;
                btn.interactable = !isFull;
                string lobbyId = room.Id;
                btn.onClick.AddListener(() =>
                {
                    string username = usernameInput != null
                        ? usernameInput.text.Trim() : "";
                    if (string.IsNullOrEmpty(username))
                    {
                        Debug.LogWarning("[LobbyUI] Username kosong!");
                        return;
                    }
                    Debug.Log($"[LobbyUI] Join room by id: {lobbyId}");
                    MultiPlayerManager.Instance.JoinRoomById(username, lobbyId);
                });
            }
        }

        if (btnRefreshRooms != null) btnRefreshRooms.interactable = true;
    }
    public void OnPrivateToggleChanged(bool isPrivate)
    {
        if (MultiPlayerManager.Instance?.CurrLobby != null)
            MultiPlayerManager.Instance.SetRoomPrivate(isPrivate);
    }

    private void OnDestroy()
    {
        if (MultiPlayerManager.Instance == null) return;
        MultiPlayerManager.Instance.OnRoomCreatedDetail -= UpdateLobbyUIHost;
        MultiPlayerManager.Instance.OnReady -= RefreshPublicRooms;
        MultiPlayerManager.Instance.ListPlayer -= UpdatePlayerList;
        MultiPlayerManager.Instance.GameStart -= EnableStartButton;
        MultiPlayerManager.Instance.OnRoomJoin -= UpdateLobbyUIClient;
        MultiPlayerManager.Instance.OnKicked -= OnKickedFromRoom;
        MultiPlayerManager.Instance.OnHostLeft -= OnHostLeftRoom;
    }

    public void ClickCreateRoom()
    {
        string username = usernameInput.text.Trim();
        string roomName = createRoomNameInput.text.Trim();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("Username atau RoomName kosong");
            return;
        }
        bool isPrivate = togglePrivate != null && togglePrivate.isOn;
        MultiPlayerManager.Instance.CreateRoom(username, roomName, selectedGhostAmount, isPrivate);
        MultiPlayerManager.Instance.UploadUsernameToLobby(username);
    }

    public void ClickJoinRoom()
    {
        string username = usernameInput.text.Trim();
        string code = joinRoomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("Username atau Code kosong");
            return;
        }
        MultiPlayerManager.Instance.JoinRoom(username, code);
        MultiPlayerManager.Instance.UploadUsernameToLobby(username);
        Debug.Log("Click Join Room");
    }


    public void ClickLeaveRoom()
    {
        bool isHost = MultiPlayerManager.Instance?.GetHost() ?? false;

        if (isHost)
        {
            MultiPlayerManager.Instance.LeaveRoom();
            nav.ShowDynamicSwitchMenu(nav.multiPlayerMode);
        }
        else
        {
            MultiPlayerManager.Instance.LeaveRoom();
            nav.ShowDynamicSwitchMenu(nav.multiPlayerMode);
        }

        Debug.Log($"[LobbyUI] Leave room as {(isHost ? "Host" : "Client")}");
    }

    public async void ClickStartGame()
    {
        int totalPlayer = MultiPlayerManager.Instance?.GetCurrPlayer() ?? 1;
        int minPlayer = selectedGhostAmount == 2 ? 4 : 2;
        if (totalPlayer < minPlayer)
        {
            popupPanel.SetActive(true);
            return;
        }
        await MultiPlayerManager.Instance.UpdateGhostAmount(selectedGhostAmount);
        await MultiPlayerManager.Instance.StartGame();
    }


    private void UpdateLobbyUIClient()
    {
        if (MultiPlayerManager.Instance.CurrLobby == null) return;
        if (MultiPlayerManager.Instance.CurrLobby.HostId == AuthenticationService.Instance.PlayerId) return;

        roomCodeTextPovClient.text = MultiPlayerManager.Instance.CurrLobby.LobbyCode;
        roomNameTextPovClient.text = MultiPlayerManager.Instance.CurrLobby.Name;

        if (ghostAmountPanel != null)
            ghostAmountPanel.SetActive(false);

        nav.ShowDynamicSwitchMenu(nav.lobbyClient);
    }
    private void UpdatePlayerList(List<string> players)
    {
        bool isHost = MultiPlayerManager.Instance?.GetHost() ?? false;
        Transform targetContent = isHost ? playerListContentHost : playerListContentClient;

        if (targetContent == null)
        {
            Debug.LogError("[LobbyUI] playerListContent null!");
            return;
        }
        if (playerNamePrefab == null)
        {
            Debug.LogError("[LobbyUI] playerNamePrefab null!");
            return;
        }

        foreach (Transform child in targetContent)
            Destroy(child.gameObject);

        string myId = AuthenticationService.Instance.PlayerId;
        var lobbyPlayers = MultiPlayerManager.Instance?.CurrLobby?.Players;
        if (lobbyPlayers == null) return;

        foreach (var player in lobbyPlayers)
        {
            string nama = player.Data.ContainsKey("UsernamePlayer")
                ? player.Data["UsernamePlayer"].Value : "Anomali";

            GameObject obj = Instantiate(playerNamePrefab, targetContent, false);
            obj.GetComponentInChildren<TextMeshProUGUI>().text = nama;

            var kickBtn = obj.GetComponentInChildren<Button>();
            if (kickBtn != null)
            {
                bool canKick = isHost && player.Id != myId;
                kickBtn.gameObject.SetActive(canKick);
                if (canKick)
                {
                    string pid = player.Id;
                    kickBtn.onClick.AddListener(() =>
                        MultiPlayerManager.Instance.KickPlayer(pid));
                }
            }
        }
    }
    private void EnableStartButton()
    {
        startButton.interactable = true;
    }
    public void SyncGenderForSinglePlayer()
    {
        selectedGender = saveMAnager.Instance?.GetGenderPlayer() ?? "female";
        SyncGenderToSession(selectedGender);
        RefreshGenderUI();
        Debug.Log("[LobbyUI] Sync gender untuk singleplayer: " + selectedGender);
    }
    public void KlikStartSinglePlayer()
    {
        selectedGender = saveMAnager.Instance?.GetGenderPlayer() ?? "female";
        SyncGenderToSession(selectedGender);
        Debug.Log("[LobbyUI] Start SinglePlayer dengan gender: " + selectedGender);

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager ga ada!");
            return;
        }
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
    public void SelectGender(string gender)
    {
        selectedGender = gender;
        if (gender == "female")
        {
            teksGender.text = "Female";
        }else
        {
            teksGender.text = "Male";
        }

        saveMAnager.Instance?.SetGender(gender);
        Debug.Log("Save" + gender);
        SyncGenderToSession(gender);
        RefreshGenderUI();
    }

    private void SyncGenderToSession(string gender)
    {
        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.LocalPlayerGender =
                gender == "male" ? PlayerGender.Male : PlayerGender.Female;
    }

    private void RefreshGenderUI()
    {
        SetButtonSelected(btnFemale, selectedGender == "female");
        SetButtonSelected(btnMale, selectedGender == "male");
    }

    public void SelectGhostPref(string pref)
    {
        selectedGhostPref = pref;
        if (pref == "random")
        {
            teksGhost.text = "Random";
        }else if (pref == "kunti")
        {
            teksGhost.text = "Kunti";
        }else if (pref == "tuyul")
        {
            teksGhost.text = "Tuyul";
        }else if (pref == "wewe")
        {
            teksGhost.text = "Wewe";
        }
        saveMAnager.Instance?.SetGhostPref(pref);
        Debug.Log("Save ghost " + pref);
        RefreshGhostPrefUI();
    }

    public void SelectItem(string itempref)
    {
        if (itempref == "notebook" && !CheckLevelRequirement(4, "Notebook")) return;
        if (itempref == "spiritbox" && !CheckLevelRequirement(8, "SpiritBox")) return;

        if (itempref == "spiritbox") teksItem.text = "SpiritBox";
        else if (itempref == "notebook") teksItem.text = "Notebook";
        saveMAnager.Instance?.SetSpecialTool(itempref);

        UploadItemToLobby(itempref);

        SetButtonSelected(btnspiritbox, itempref == "spiritbox");
        SetButtonSelected(btnnotebook, itempref == "notebook");

        Debug.Log($"[LobbyUI] Select Item: {itempref}");
    }
    private async void UploadItemToLobby(string itemKey)
{
    var lobby = MultiPlayerManager.Instance?.CurrLobby;
    if (lobby == null) return;
    try
    {
        await LobbyService.Instance.UpdatePlayerAsync(
            lobby.Id,
            AuthenticationService.Instance.PlayerId,
            new Unity.Services.Lobbies.UpdatePlayerOptions
            {
                Data = new Dictionary<string, Unity.Services.Lobbies.Models.PlayerDataObject>
                {
                    {
                        "StarterItem",
                        new Unity.Services.Lobbies.Models.PlayerDataObject(
                            Unity.Services.Lobbies.Models.PlayerDataObject.VisibilityOptions.Member,
                            itemKey)
                    }
                }
            });
        Debug.Log($"[LobbyUI] StarterItem uploaded: {itemKey}");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[LobbyUI] Gagal upload StarterItem: {e.Message}");
    }
}
    
    private void RefreshGhostPrefUI()
    {
        SetButtonSelected(btnRandom, selectedGhostPref == "random");
        SetButtonSelected(btnKunti, selectedGhostPref == "kunti");
        SetButtonSelected(btnTuyul, selectedGhostPref == "tuyul");
        SetButtonSelected(btnWewe, selectedGhostPref == "wewe");
    }

    private void SelectGhostAmount(int amount)
    {
        selectedGhostAmount = amount;
        RefreshGhostAmountUI();
    }

    private void RefreshGhostAmountUI()
    {
        SetButtonSelected(btnGhost1, selectedGhostAmount == 1);
        SetButtonSelected(btnGhost2, selectedGhostAmount == 2);
    }

    private void SetButtonSelected(Button btn, bool selected)
    {
        if (btn == null) return;
    }
    private void OnKickedFromRoom()
    {
        nav.ShowDynamicSwitchMenu(nav.multiPlayerMode);
        popupKickedPanel.SetActive(true);
    }

    private void OnHostLeftRoom()
    {
        nav.ShowDynamicSwitchMenu(nav.multiPlayerMode);
        popupHostLeftPanel.SetActive(true);
    }

    private void ToggleOuija()
    {
        if (!OuijaSettings.IsActive && !CheckLevelRequirement(14, "Ouija Board")) return;

        OuijaSettings.IsActive = !OuijaSettings.IsActive;
        RefreshOuijaUI();
        UploadOuijaToLobby(OuijaSettings.IsActive);
        Debug.Log($"[LobbyUI] Ouija toggle: {OuijaSettings.IsActive}");
    }

    private void RefreshOuijaUI()
{
    if (teksOuija != null)
        teksOuija.text = OuijaSettings.IsActive ? "Active" : "Inactive";
    SetButtonSelected(btnOuija, OuijaSettings.IsActive);
}

private async void UploadOuijaToLobby(bool active)
{
    var lobby = MultiPlayerManager.Instance?.CurrLobby;
    if (lobby == null) return;
    try
    {
        await LobbyService.Instance.UpdateLobbyAsync(lobby.Id,
            new Unity.Services.Lobbies.UpdateLobbyOptions
            {
                Data = new Dictionary<string, Unity.Services.Lobbies.Models.DataObject>
                {
                    {
                        "OuijaActive",
                        new Unity.Services.Lobbies.Models.DataObject(
                            Unity.Services.Lobbies.Models.DataObject.VisibilityOptions.Member,
                            active ? "true" : "false")
                    }
                }
            });
        Debug.Log($"[LobbyUI] OuijaActive uploaded: {active}");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[LobbyUI] Gagal upload OuijaActive: {e.Message}");
    }
}

    private void RefreshAllFromSave()
    {
        if (saveMAnager.Instance == null) return;

        selectedGender = saveMAnager.Instance.GetGenderPlayer() ?? "female";
        if (teksGender != null)
            teksGender.text = selectedGender == "male" ? "Male" : "Female";
        RefreshGenderUI();

        selectedGhostPref = saveMAnager.Instance.GetGhostPref() ?? "random";
        if (teksGhost != null)
            teksGhost.text = selectedGhostPref switch
            {
                "kunti" => "Kunti",
                "tuyul" => "Tuyul",
                "wewe" => "Wewe",
                _ => "Random"
            };
        RefreshGhostPrefUI();

        string item = saveMAnager.Instance.GetSpecialTool() ?? "none";
        if (teksItem != null)
            teksItem.text = item switch
            {
                "spiritbox" => "SpiritBox",
                "notebook" => "Notebook",
                _ => "-"
            };
        SetButtonSelected(btnspiritbox, item == "spiritbox");
        SetButtonSelected(btnnotebook, item == "notebook");

        if (teksLevel != null)
            teksLevel.text = saveMAnager.Instance.GetLevel().ToString();

        if (teksUsername != null)
            teksUsername.text = saveMAnager.Instance.GetUsernamePlayer() ?? "-";

        RefreshStats();
    }

    public void RefreshStats()
    {
        if (saveMAnager.Instance == null) return;
        if (matchSingleText != null) matchSingleText.text = saveMAnager.Instance.GetTotalSingleMatch().ToString();
        if (matchMultiText != null) matchMultiText.text = saveMAnager.Instance.GetTotalMultiMatch().ToString();
        if (winSingleText != null) winSingleText.text = saveMAnager.Instance.GetTotalSingleWins().ToString();
        if (winMultiText != null) winMultiText.text = saveMAnager.Instance.GetTotalMultiWins().ToString();
    }
}