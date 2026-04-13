using Unity.Netcode;
using UnityEngine;
using TMPro;

public class CheatManager : NetworkBehaviour
{
    public static CheatManager Instance;

    private string _inputBuffer = "";
    private const int MAX_BUFFER = 20;

    [Header("Item Clue UI")]
    public GameObject itemClueButton;
    public GameObject itemCluePanel;
    public TextMeshProUGUI itemClueText;
    public TextMeshProUGUI level;

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip cheat251Clip;

    public bool isOopRemed = false;

    public bool isClueaktif = false;
    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    private void Update()
    {
        foreach (char c in Input.inputString)
        {
            _inputBuffer += char.ToLower(c);
            if (_inputBuffer.Length > MAX_BUFFER)
                _inputBuffer = _inputBuffer.Substring(_inputBuffer.Length - MAX_BUFFER);
            CheckCheats();
        }
    }

    private void CheckCheats()
    {
        if (_inputBuffer.EndsWith("kesadarandiri"))
        {
            _inputBuffer = "";
            Debug.Log("Cheat: banishment items");
            TriggerCheatSpawnBanishment();
        }

        if (_inputBuffer.EndsWith("dualimasatu"))
        {
            sfxSource.PlayOneShot(cheat251Clip);
            _inputBuffer = "";
            saveMAnager.Instance.SetLevel(251);

            if (level != null) level.text = "251";

            var lobbyUI = FindFirstObjectByType<LobbyUIController>();
            if (lobbyUI != null)
            {
                lobbyUI.RefreshStats();
                if (lobbyUI.teksLevel != null) lobbyUI.teksLevel.text = "251";
            }

            Debug.Log("Cheat dua lima satu");
        }

        if (_inputBuffer.EndsWith("demonlord"))
        {
            _inputBuffer = "";
            Debug.Log("Cheat demonlord: skip phase");

            if (GamePhaseManager.Instance != null)
                GamePhaseManager.Instance.CheatSkipPhase();
        }
        if (_inputBuffer.EndsWith("oopremed"))
        {
            _inputBuffer = "";
            Debug.Log("Cheat oopremed");
            isOopRemed = true;
        }
        if (_inputBuffer.EndsWith("ej261"))
        {
            _inputBuffer = "";
            Debug.Log("Cheat ritualstart: start ritual");
            TriggerForceRitual();
        }
        if (_inputBuffer.EndsWith("resetlevel"))
        {
            _inputBuffer = "";
            saveMAnager.Instance.SetLevel(1);
            if (level != null) level.text = "1";

            var lobbyUI = FindFirstObjectByType<LobbyUIController>();
            if (lobbyUI != null && lobbyUI.teksLevel != null)
                lobbyUI.teksLevel.text = "1";

            Debug.Log("[Cheat] Level reset ke 1");
        }

        if (_inputBuffer.EndsWith("resetstats"))
        {
            _inputBuffer = "";
            saveMAnager.Instance?.AddMatchResult(false, false);
            saveMAnager.Instance?.ResetStats();

            var lobbyUI = FindFirstObjectByType<LobbyUIController>();
            lobbyUI?.RefreshStats();

            Debug.Log("[Cheat] Stats direset");
        }
    }
    private void TriggerForceRitual()
    {
        if (RitualManager.Instance == null)
        {
            Debug.LogWarning("[CheatManager] RitualManager gadaa");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            RitualManager.Instance.CheatForceStartRitual();
        else
            ForceRitualServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ForceRitualServerRpc()
    {
        RitualManager.Instance?.CheatForceStartRitual();
    }

    private void TriggerCheatSpawnBanishment()
    {
        if (ItemSpawner.Instance == null)
        {
            Debug.LogWarning("[CheatManager] ItemSpawner instancenya null!");
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            ItemSpawner.Instance.CheatSpawnBanishment();
        }
        else
        {
            CheatSpawnServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CheatSpawnServerRpc()
    {
        if (ItemSpawner.Instance == null)
        {
            Debug.LogWarning("[CheatManager] ServerRpc: ItemSpawner instnc nu");
            return;
        }
        ItemSpawner.Instance.CheatSpawnBanishment();
    }



}