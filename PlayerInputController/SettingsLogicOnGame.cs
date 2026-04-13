using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SettingsLogicOnGame : MonoBehaviour
{
    public bool isSettings = false;
    public static SettingsLogicOnGame instance;

    [SerializeField] private Slider sliderFOV;
    [SerializeField] private Slider sliderVolume;
    [SerializeField] private Slider sliderSensitivity;
    [SerializeField] private Button btnBackLobby;
    [SerializeField] private Button btnBackTitle;
    [SerializeField] private Button btnBack;
    [SerializeField] private GameObject settingsPanelSingle;
    [SerializeField] private GameObject settingsPanelMulti;
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private TextMeshProUGUI labelFOV;
    [SerializeField] private TextMeshProUGUI labelVolume;
    [SerializeField] private TextMeshProUGUI labelSensitivity;

    [SerializeField] private Button btnMute;

    private bool isInitializing = false;
    private GameObject _curActivePanel;
    private bool _isLoadingScene = false;

    private void Awake()
    {
        instance = this;
        isInitializing = true;

        if (settingsPanelSingle != null) settingsPanelSingle.SetActive(true);
        if (settingsPanelMulti != null) settingsPanelMulti.SetActive(true);

        sliderVolume.minValue = 0f;
        sliderVolume.maxValue = 100f;
        sliderSensitivity.minValue = 1f;
        sliderSensitivity.maxValue = 100f;
        sliderFOV.minValue = 10f;
        sliderFOV.maxValue = 100f;

        sliderVolume.value = saveMAnager.Instance.GetVolume();
        sliderSensitivity.value = saveMAnager.Instance.GetSensitivity();
        sliderFOV.value = saveMAnager.Instance.GetFoV();

        UpdateLabels();
        isInitializing = false;

        sliderVolume.onValueChanged.RemoveAllListeners();
        sliderSensitivity.onValueChanged.RemoveAllListeners();
        sliderFOV.onValueChanged.RemoveAllListeners();
        sliderVolume.onValueChanged.AddListener(OnVolumeChanged);
        sliderSensitivity.onValueChanged.AddListener(OnSensitivityChanged);
        sliderFOV.onValueChanged.AddListener(OnFOVChanged);

        btnBack.onClick.RemoveAllListeners();
        btnBackLobby.onClick.RemoveAllListeners();
        btnBackTitle.onClick.RemoveAllListeners();
        btnBack.onClick.AddListener(OnClickBack);
        btnBackLobby.onClick.AddListener(OnClickBackLobby);
        btnBackTitle.onClick.AddListener(OnClickBackTitle);

        btnMute?.onClick.RemoveAllListeners();
        btnMute?.onClick.AddListener(OnClickMute);
        UpdateMuteButtonLabel();
    }

    private void Start()
    {
        settingsPanelSingle?.SetActive(false);
        settingsPanelMulti?.SetActive(false);
        isSettings = false;
    }

    public void ToggleSettings()
    {
        if (_isLoadingScene) return;

        isSettings = !isSettings;

        bool isMulti = MultiPlayerManager.Instance?.CurrentGameMode == GameMode.Multiplayer;
        GameObject targetPanel = isMulti ? settingsPanelMulti : settingsPanelSingle;
        _curActivePanel = targetPanel;

        settingsPanelSingle?.SetActive(false);
        settingsPanelMulti?.SetActive(false);
        targetPanel?.SetActive(isSettings);

        Cursor.lockState = isSettings ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isSettings;
    }

    private void OnClickBack()
    {
        isSettings = false;
        _curActivePanel?.SetActive(false);
        _curActivePanel = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnClickBackLobby()
    {
        if (_isLoadingScene) return;
        ExitToMenu("Lobby");
    }

    public void OnClickBackTitle()
    {
        if (_isLoadingScene) return;
        ExitToMenu("Title");
    }

    private void ExitToMenu(string targetPanel)
    {
        if (_isLoadingScene) return;
        _isLoadingScene = true;

        if (MultiPlayerManager.Instance?.CurrentGameMode == GameMode.Multiplayer)
            GameNetworkEvents.Instance?.BroadcastHostLeft();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        MainMenuSceneController.TargetPanel = targetPanel;
        StartCoroutine(ShutdownThenLoad());
    }
    private IEnumerator ShutdownThenLoad()
    {
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
            yield return new WaitForSeconds(0.3f);
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    private void UpdateLabels()
    {
        if (labelVolume != null)
        {
            labelVolume.text = $"{(int)sliderVolume.value}";
        }
        if (labelSensitivity != null)
        {
            labelSensitivity.text = $"{(int)sliderSensitivity.value}";
        }
        if (labelFOV != null)
        {
            labelFOV.text = $"{(int)sliderFOV.value}";
        }
    }

    private void OnVolumeChanged(float value)
    {
        if (isInitializing) return;
        AudioListener.volume = value / 100f;
        saveMAnager.Instance.SetVolume(value);

        if (labelVolume != null)
        {
            labelVolume.text = $"{(int)value}";
        }
    }

    private void OnSensitivityChanged(float value)
    {
        if (isInitializing) return;
        foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (p.IsOwner) { p.sensitivity = value; break; }
        }
        saveMAnager.Instance.SetSensitivity(value);
        if (labelSensitivity != null)
        {
            labelSensitivity.text = $"{(int)value}";
        }
    }

    private void OnFOVChanged(float value)
    {
        if (isInitializing) return;
        foreach (var p in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            if (p.IsOwner) { p.ApplyFOV(value); break; }
        }
        saveMAnager.Instance.SetFoV(value);
        if (labelFOV != null)
        {
            labelFOV.text = $"{(int)value}";
        }
    }

    public void OnClickMute()
    {
        if (VivoxManager.Instance == null) return;
        VivoxManager.Instance.SetMute(!VivoxManager.Instance.IsMuted);
        UpdateMuteButtonLabel();
    }

    private void UpdateMuteButtonLabel()
    {
        if (btnMute == null || VivoxManager.Instance == null) 
            return;
        var label = btnMute.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = VivoxManager.Instance.IsMuted ? "Unmute" : "Mute";
    }
}