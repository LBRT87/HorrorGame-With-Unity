using System.Collections;
using System.IO;
using UnityEngine;

public class saveMAnager : MonoBehaviour
{
    public static saveMAnager Instance;

    private string path;
    private gameDataFileSave currDataGame;
    private float autoSaveTimer = 240f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            path = Application.persistentDataPath + "/fileSave.dat";
            LoadGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start() => LoadGame();

    private void Update()
    {
        autoSaveTimer -= Time.deltaTime;
        if (autoSaveTimer <= 0f)
        {
            autoSaveTimer = 240f;
            SaveGame();
        }
    }

    public void SaveGame()
    {
        if (currDataGame == null) return;
        currDataGame.lastUpdated = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string json = JsonUtility.ToJson(currDataGame, true);
        File.WriteAllText(path, EncryptionGameData.Enkripsi(json));
        Debug.Log("[SaveManager] Game Saved");
    }

    public void LoadGame()
    {
        if (!File.Exists(path)) { currDataGame = new gameDataFileSave(); return; }
        string json = EncryptionGameData.Dekripsi(File.ReadAllText(path));
        currDataGame = JsonUtility.FromJson<gameDataFileSave>(json);
        Debug.Log("[SaveManager] Game Loaded");
    }
    public string GetUsernamePlayer() => currDataGame?.username;
    public float GetVolume() => currDataGame?.volume ?? 70f;
    public float GetSensitivity() => currDataGame?.sensitivity ?? 30f;
    public float GetFoV() => currDataGame?.fieldOfView ?? 84f;
    public int GetLevel() => currDataGame?.level ?? 1;
    public float GetExp() => currDataGame?.exp ?? 0f;
    public int GetTotalSingleMatch() => currDataGame?.totalSingleMatch ?? 0;
    public int GetTotalMultiMatch() => currDataGame?.totalMultiMatch ?? 0;
    public int GetTotalSingleWins() => currDataGame?.totalSingleWins ?? 0;
    public int GetTotalMultiWins() => currDataGame?.totalMultiWins ?? 0;

    public string GetStarterItem() => currDataGame?.specialTool ?? "none";
    public string GetSpecialTool() => GetStarterItem(); 
    public void SetSpecialTool(string v) { Data.specialTool = v; SaveGame(); }

    public bool GetOuijaBoardActive() => currDataGame?.ouijaActive ?? false;
    public bool GetOuijaActive() => GetOuijaBoardActive(); 
    public void SetOuijaBoardActive(bool val) { Data.ouijaActive = val; SaveGame(); }
    public void SetOuija(bool v) => SetOuijaBoardActive(v); 
    public string GetGhostPref()
        => PlayerPrefs.GetString("GhostPref", "random");

    public void SetGhostPref(string pref)
    {
        PlayerPrefs.SetString("GhostPref", pref);
        PlayerPrefs.Save();
        Debug.Log("[saveMAnager] Ghost pref: " + pref);

        if (MultiPlayerManager.Instance?.CurrLobby != null)
            MultiPlayerManager.Instance.UploadMyGhostPrefPublic();
        Debug.Log("ghost pref upload ke lobby" + pref);
    }

    public string GetGenderPlayer()
        => PlayerPrefs.GetString("PlayerGender", "female");

    public void SetGender(string gender)
    {
        PlayerPrefs.SetString("PlayerGender", gender);
        PlayerPrefs.Save();
        Debug.Log("[saveMAnager] Gender: " + gender);
    }

    public void SetUsername(string nama)
    {
        Data.username = nama;
        SaveGame();
    }
    public void SetVolume(float v) { Data.volume = v; SaveGame(); }
    public void SetSensitivity(float v) { Data.sensitivity = v; SaveGame(); }
    public void SetFoV(float v) { Data.fieldOfView = v; SaveGame(); }

    public System.Action<int> OnLevelUp;

    public void AddExp(float amount)
    {
        if (currDataGame == null) return;
        currDataGame.exp += amount;

        while (currDataGame.exp >= 1f)
        {
            currDataGame.exp -= 1f;
            currDataGame.level++;
            Debug.Log("[SaveManager] Level Up Level = " + currDataGame.level);
            OnLevelUp?.Invoke(currDataGame.level);
        }
        SaveGame();
    }

    public void AddExpByResult(bool isSuccess, bool isMultiplayer)
    {
        if (currDataGame == null) return;
        int lv = currDataGame.level;
        float gain;
        if (!isSuccess) gain = 1f / lv;
        else if (!isMultiplayer) gain = 2f / lv;
        else gain = 3f / lv;
        AddExp(gain);
    }

    public void AddMatchResult(bool isMultiplayer, bool isWin)
    {
        if (currDataGame == null) return;
        if (isMultiplayer) { currDataGame.totalMultiMatch++; if (isWin) currDataGame.totalMultiWins++; }
        else { currDataGame.totalSingleMatch++; if (isWin) currDataGame.totalSingleWins++; }
        SaveGame();
    }

    public void SetLevel(int lv) { Data.level = lv; Data.exp = 0f; SaveGame(); }

    private gameDataFileSave Data => currDataGame ??= new gameDataFileSave();

    public void ResetStats()
    {
        if (currDataGame == null) return;
        currDataGame.totalSingleMatch = 0;
        currDataGame.totalMultiMatch = 0;
        currDataGame.totalSingleWins = 0;
        currDataGame.totalMultiWins = 0;
        currDataGame.exp = 0f;
        SaveGame();
        Debug.Log("[SaveManager] Stats direset");
    }
}