using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TextMeshProUGUI MessageText;
    public GameObject MessagePanel;

    public Slider healthSlider;

    public Image bloodScreenFull;
    public Image bloodScreenAlmostDead;
    public GameObject deathPopup;
    public GameObject winPopup;

    public TextMeshProUGUI timeText;
    public TextMeshProUGUI phaseText;

    public GameObject ritualProgressBarObj;
    public Slider ritualSlider;

    public GameObject countdownBarObj;
    public Slider countDownSlider;
    public TextMeshProUGUI countdownText;

    public GameObject breathBarObj;
    public Slider breathSlider;

    public GameObject slot1Obj;
    public GameObject slot2Obj;
    public Image slot1Icon;
    public Image slot2Icon;

    public GameObject healPanelObj;
    public Slider healProgressSlider;

    public GameObject itemClueButton;
    public GameObject itemCluePanel;
    public TextMeshProUGUI itemClueText;

    public GameObject paperReadPanel;
    public TextMeshProUGUI paperGhostHintText;
    public TextMeshProUGUI paperItemHintText;

    public GameObject disconnectPanel;
    public TextMeshProUGUI disconnectTitleText;
    public GameObject controlUIobj;
    public bool isControl;

    private static readonly string[] PhaseNames = {
        "", "Phase 1: Wandering", "Phase 2: Disturbing",
        "Phase 3: Hunting", "Last Minute"
    };

    private HealthSystem _localHealth;
    void Start()
    {

        SetActive(MessagePanel, false);
        SetActive(ritualProgressBarObj, false);
        SetActive(countdownBarObj, false);
        SetActive(breathBarObj, false);
        SetActive(deathPopup, false);
        SetActive(healPanelObj, false);
        SetActive(paperReadPanel, false);
        SetActive(controlUIobj, false);
        SetActive(disconnectPanel, false);

        if (bloodScreenFull != null) bloodScreenFull.gameObject.SetActive(false);
        if (bloodScreenAlmostDead != null) bloodScreenAlmostDead.gameObject.SetActive(false);
        SetActive(slot1Obj, false);
        SetActive(slot2Obj, false);

        if (healthSlider != null)
        {
            healthSlider.minValue = 0;
            healthSlider.maxValue = HealthSystem.MAX_HEALTH;
            healthSlider.value = HealthSystem.MAX_HEALTH;
        }
        if (ritualSlider != null) { ritualSlider.minValue = 0; ritualSlider.maxValue = 1; }
        if (breathSlider != null) { breathSlider.minValue = 0; breathSlider.maxValue = 1; breathSlider.value = 1; }
        if (healProgressSlider != null) { healProgressSlider.minValue = 0; healProgressSlider.maxValue = 1; }

        if (HUDManager.Instance != null)
            HUDManager.Instance.RegisterExorcistHUD(this);

        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.OnHostLeft += ShowHostLeftPopup;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void Update()
    {
        if (CheatManager.Instance != null && CheatManager.Instance.isOopRemed)
        {
            if (itemClueButton != null && !itemClueButton.activeSelf)
            {
                itemClueButton.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Debug.Log("HUD: Cheat oopremed terdeteksi");
            }
        }
    }

    public void RegisterHealthSystem(HealthSystem hs)
    {
        if (_localHealth != null)
            _localHealth.OnHealthChanged -= OnHealthChanged;
        _localHealth = hs;
        _localHealth.OnHealthChanged += OnHealthChanged;
        OnHealthChanged(_localHealth.currentHealth.Value);
    }

    private void OnHealthChanged(int newHp)
    {
        if (healthSlider != null) healthSlider.value = newHp;
        if (bloodScreenFull != null) bloodScreenFull.gameObject.SetActive(newHp == 2);
        if (bloodScreenAlmostDead != null) bloodScreenAlmostDead.gameObject.SetActive(newHp == 1);
        if (newHp <= 0) ShowDeathPopup();
    }

    public void ShowDeathPopup()
    {
        SetActive(deathPopup, true);
        HUDManager.Instance?.OnGameEnded();
    }

    public void ShowWinPopup()
    {
        SetActive(winPopup, true);
        HUDManager.Instance?.OnGameEnded();
    }


    public void OpenControlsUI()
    {
        if (controlUIobj == null) return;
        isControl = !isControl;
        controlUIobj.SetActive(isControl);
        Cursor.lockState = isControl ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isControl;
    }

    public void BackBtnControl()
    {
        SetActive(controlUIobj, false);
        isControl = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnClickBackToLobby()
    {
        var helper = FindFirstObjectByType<GameExitHelper>();
        if (helper != null)
            helper.GoToLobby();
        else
            StartCoroutine(FallbackExit("Lobby"));
    }

    public void OnClickBackToTitle()
    {
        var helper = FindFirstObjectByType<GameExitHelper>();
        if (helper != null)
            helper.GoToTitle();
        else
            StartCoroutine(FallbackExit("Title"));
    }

    private IEnumerator FallbackExit(string panel)
    {
        Time.timeScale = 1f;
        MainMenuSceneController.TargetPanel = panel;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        yield return new WaitForSeconds(0.5f);
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private System.Collections.IEnumerator LoadAfterShutdown(string sceneName)
    {
        yield return new WaitForSeconds(0.3f);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
    private void OnDestroy()
    {
        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.OnHostLeft -= ShowHostLeftPopup;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
    private void OnClientDisconnected(ulong clientId)
    {
        bool isLocal = clientId == NetworkManager.Singleton.LocalClientId;
        bool isServerGone = !NetworkManager.Singleton.IsConnectedClient
                            && !NetworkManager.Singleton.IsHost;

        if (isLocal || isServerGone)
        {
            ShowDisconnectPopup();

        }
    }
    public void ShowHostLeftPopup()
    {
        ShowDisconnectPopup();
    }

    public void ShowDisconnectPopup()
    {
        if (disconnectPanel == null) return;

        disconnectPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void UpdateTime(string formattedTime)
    {
        if (timeText != null) timeText.text = formattedTime;
    }

    public void UpdatePhase(int phase)
    {
        if (phaseText == null) return;
        if (phase >= 1 && phase <= 4)
        {
            phaseText.text = PhaseNames[phase];
        }
    }

    public void ShowRitualBar() => SetActive(ritualProgressBarObj, true);
    public void HideRitualBar()
    {
        SetActive(ritualProgressBarObj, false);
        if (ritualSlider != null) 
            ritualSlider.value = 0;
    }
    public void UpdateRitualProgress(float value)
    {
        if (ritualSlider != null)
        {
            ritualSlider.value = value;
        }
    }

    public void ShowCountdown()
    {
        SetActive(countdownBarObj, true);
        if (countDownSlider != null)
        {
            countDownSlider.minValue = 0f;
            countDownSlider.maxValue = 1f;
            countDownSlider.value = 1f;
        }
    }
    public void UpdateCountdown(string text)
    {
        if (countdownText != null)
        {
            countdownText.text = text;
        }
        if (countDownSlider != null && float.TryParse(text, out float remaining))
        {

            countDownSlider.value = Mathf.Clamp01(remaining / 60f);
        }
    }

    public void ShowBreathBar() => SetActive(breathBarObj, true);
    public void HideBreathBar() => SetActive(breathBarObj, false);
    public void UpdateBreath(float normalized)
    {
        if (breathSlider != null) breathSlider.value = normalized;
    }

    public void UpdateSlot(int slotIndex, bool hasItem)
    {
        if (slotIndex == 0) SetActive(slot1Obj, hasItem);
        else if (slotIndex == 1) SetActive(slot2Obj, hasItem);
    }

    public void UpdateHealthBar(float normalized)
    {
        if (healthSlider != null) healthSlider.value = normalized * HealthSystem.MAX_HEALTH;
    }

    public void ShowHealProgress(float progress)
    {
        if (healPanelObj != null) { 
            healPanelObj.SetActive(true); 
        }
        if (healProgressSlider != null)
        {
            healProgressSlider.value = progress;
        }
    }
    public void HideHealProgress()
    {
        if (healPanelObj != null)
        {
            healPanelObj.SetActive(false);
        }
        if (healProgressSlider != null) { 
            healProgressSlider.value = 0f; 
        }
    }
    public void ShowHealPanel()
    {
        if (healPanelObj != null)
        {
            healPanelObj.SetActive(true);

        }
        if (healProgressSlider != null)
        {
            healProgressSlider.value = 0f;

        }
    }
    public void HideHealPanel()
    {
        if (healPanelObj != null) healPanelObj.SetActive(false);
        if (healProgressSlider != null) healProgressSlider.value = 0f;
    }

    public void OpenMessagePanel(string teks)
    {
        if (MessagePanel == null || MessageText == null) return;
        MessageText.text = teks;
        MessagePanel.SetActive(true);
    }
    public void CloseMessagePanel() => SetActive(MessagePanel, false);

    public void OpenDisconnectPanel()
    {
        ShowDisconnectPopup();
    }
    public void CloseDisconnectPanel() => SetActive(disconnectPanel, false);

    public void ShowPaperPanel(string ghostHint, string itemHint)
    {
        if (paperReadPanel == null) return;
        if (paperGhostHintText != null) 
            paperGhostHintText.text = ghostHint;
        if (paperItemHintText != null) 
            paperItemHintText.text = itemHint;

        paperReadPanel.SetActive(true);
    }
    public void HidePaperPanel() => SetActive(paperReadPanel, false);
    public void OnClickClosePaper() => HidePaperPanel();

    public void KlikClueButton()
    {
        if (itemCluePanel == null) return;

        bool isOpening = !itemCluePanel.activeSelf;
        itemCluePanel.SetActive(isOpening);

        if (isOpening)
        {
            RefreshItemClueText();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    

    private void RefreshItemClueText()
    {
        if (itemClueText == null) return;
        var registry = GameRecipeRegistry.Instance;
        if (registry == null) { 
            itemClueText.text = "registry blum siap"; 
            return;
        }

        string text = "Banishment Items:\n";
        foreach (var ghost in registry.GetActiveGhosts())
            foreach (var item in registry.GetRecipeFor(ghost))
                text += $"> {item}\n";

        itemClueText.text = text.TrimEnd();
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

}