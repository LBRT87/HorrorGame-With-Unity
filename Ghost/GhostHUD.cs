using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GhostHUD : MonoBehaviour
{
    public TextMeshProUGUI namaGhostText;

    public GameObject infoskillkuntilanak;
    public GameObject infoskillwewegombel;
    public GameObject infoskilltuyul;

    public GameObject specialskill_kuntilanak;
    public GameObject specialskill_wewegombel;
    public GameObject specialskill_tuyul;

    public GameObject teleportIcon;
    public GameObject unhideIcon;
    public GameObject screamIcon;

    public GameObject bgcooldown_teleport;
    public GameObject bgcooldown_hide;
    public GameObject bgcooldown_scream;
    public GameObject bgcooldown_special;
    public GameObject bgcooldown_attack;

    public TextMeshProUGUI cdTextTeleport;
    public TextMeshProUGUI cdTextHide;
    public TextMeshProUGUI cdTextScream;
    public TextMeshProUGUI cdTextSpecial;
    public TextMeshProUGUI cdTextAttack;

    public TextMeshProUGUI timeText;
    public TextMeshProUGUI phaseText;
    public Slider ritualBar;
    public GameObject ritualBarObj;

    public Slider countDownSlider;
    public GameObject countdownBarObj;
    public TextMeshProUGUI countdownText;

    public GameObject popupGhostWin;
    public GameObject popupGhostLose;

    public GameObject attackModeIcon;

    public GameObject controlsPanel;

    public GameObject recipePanel;
    public TextMeshProUGUI recipeText;

    public TextMeshProUGUI MessageText;
    public GameObject MessagePanel;

    public GameObject holdProgressObj;
    public Slider holdProgressSlider;

    public GameObject disconnectPanel;
    public TextMeshProUGUI disconnectMessageText;

    public enum SkillType { Teleport, Hide, Scream, Special, Attack }

    private const float MAX_CD_TELEPORT = 20f;
    private const float MAX_CD_HIDE = 20f;
    private const float MAX_CD_SCREAM = 15f;
    private const float MAX_CD_SPECIAL = 35f;
    private const float MAX_CD_ATTACK = 30f;

    private GhostType _myGhostType;
    private GhostType _myGhostTypeEnum;
    private GameObject _activeInfoPanel;
    private bool _infoVisible = false;

    private float _cdTeleport, _cdHide, _cdScream, _cdSpecial, _cdAttack;
    private float _maxTeleport = MAX_CD_TELEPORT;
    private float _maxHide = MAX_CD_HIDE;
    private float _maxScream = MAX_CD_SCREAM;
    private float _maxSpecial = MAX_CD_SPECIAL;
    private float _maxAttack = MAX_CD_ATTACK;

    private bool _attackModeEnabled = false;

    private void Start()
    {
        SetActive(infoskillkuntilanak, false);
        SetActive(infoskillwewegombel, false);
        SetActive(infoskilltuyul, false);
        SetActive(specialskill_kuntilanak, false);
        SetActive(specialskill_wewegombel, false);
        SetActive(specialskill_tuyul, false);
        SetActive(bgcooldown_teleport, false);
        SetActive(bgcooldown_hide, false);
        SetActive(bgcooldown_scream, false);
        SetActive(bgcooldown_special, false);
        SetActive(bgcooldown_attack, false);
        SetActive(popupGhostWin, false);
        SetActive(popupGhostLose, false);
        SetActive(ritualBarObj, false);
        SetActive(countdownBarObj, false);
        SetActive(holdProgressObj, false);
        SetActive(attackModeIcon, false);
        SetActive(controlsPanel, false);
        SetActive(recipePanel, true);
        SetActive(disconnectPanel, false);

        if (holdProgressSlider != null) { holdProgressSlider.minValue = 0; holdProgressSlider.maxValue = 1; }
        if (ritualBar != null) { ritualBar.minValue = 0; ritualBar.maxValue = 1; }

        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.OnHostLeft += ShowHostLeftPopup;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (MultiPlayerManager.Instance != null)
            MultiPlayerManager.Instance.OnHostLeft -= ShowHostLeftPopup;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Update()
    {
        TickCooldownUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        bool isLocal = clientId == NetworkManager.Singleton.LocalClientId;
        bool isServerGone = !NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost;

        if (isLocal || isServerGone)
            ShowDisconnectPopup("Disconnected", "You Disconnected");
    }

    public void ShowHostLeftPopup()
        => ShowDisconnectPopup("Host Left", "Disconnected");

    public void ShowDisconnectPopup(string title, string message)
    {
        if (disconnectPanel == null) return;

        if (disconnectMessageText != null) disconnectMessageText.text = message;

        disconnectPanel.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void InitForGhost(GhostType type,
                             float maxTeleport = 20,
                             float maxHide = 20,
                             float maxScream = 15,
                             float maxSpecial = 35,
                             float maxAttack = 30)
    {
        _myGhostType = type;
        _myGhostTypeEnum = type;
        _maxTeleport = maxTeleport;
        _maxHide = maxHide;
        _maxScream = maxScream;
        _maxSpecial = maxSpecial;

        if (namaGhostText != null)
            namaGhostText.text = type switch
            {
                GhostType.Kuntilanak => "Kuntilanak",
                GhostType.Tuyul => "Tuyul",
                GhostType.WeweGombel => "Wewe Gombel",
                _ => "Ghost"
            };

        SetActive(infoskillkuntilanak, false);
        SetActive(infoskillwewegombel, false);
        SetActive(infoskilltuyul, false);
        SetActive(specialskill_kuntilanak, false);
        SetActive(specialskill_wewegombel, false);
        SetActive(specialskill_tuyul, false);

        switch (type)
        {
            case GhostType.Kuntilanak:
                _activeInfoPanel = infoskillkuntilanak;
                SetActive(specialskill_kuntilanak, true);
                break;
            case GhostType.Tuyul:
                _activeInfoPanel = infoskilltuyul;
                SetActive(specialskill_tuyul, true);
                break;
            case GhostType.WeweGombel:
                _activeInfoPanel = infoskillwewegombel;
                SetActive(specialskill_wewegombel, true);
                break;
        }

        SetActive(_activeInfoPanel, false);
        _infoVisible = false;
    }

    public void UpdatePhase(int phase)
    {
        Debug.Log($"[GhostHUD] UpdatePhase phase={phase}");

        if (phaseText != null) phaseText.text = $"Phase {phase}";
        else Debug.LogWarning("[GhostHUD] phaseText null");

        if (phase >= 2) ShowRecipeForPhase2();

        _attackModeEnabled = phase >= 3;
        if (!_attackModeEnabled) SetAttackMode(false);
    }

    public void SetAttackMode(bool active)
    {
        if (active && !_attackModeEnabled)
        {
            Debug.Log("[GhostHUD] Attack mode belum available");
            return;
        }
        if (attackModeIcon != null) attackModeIcon.SetActive(active);
    }

    public void OnAttackExecuted()
    {
        if (!_attackModeEnabled) return;
        StartCooldown(SkillType.Attack);
        SetAttackMode(false);
    }

    public bool IsAttackModeEnabled => _attackModeEnabled;

    public void StartCooldown(SkillType skill)
    {
        switch (skill)
        {
            case SkillType.Teleport:
                _cdTeleport = _maxTeleport;
                SetActive(bgcooldown_teleport, true);
                if (cdTextTeleport) cdTextTeleport.text = Mathf.CeilToInt(_cdTeleport).ToString();
                break;
            case SkillType.Hide:
                _cdHide = _maxHide;
                SetActive(bgcooldown_hide, true);
                if (cdTextHide) cdTextHide.text = Mathf.CeilToInt(_cdHide).ToString();
                break;
            case SkillType.Scream:
                _cdScream = _maxScream;
                SetActive(bgcooldown_scream, true);
                if (cdTextScream) cdTextScream.text = Mathf.CeilToInt(_cdScream).ToString();
                break;
            case SkillType.Special:
                _cdSpecial = _maxSpecial;
                SetActive(bgcooldown_special, true);
                if (cdTextSpecial) cdTextSpecial.text = Mathf.CeilToInt(_cdSpecial).ToString();
                break;
            case SkillType.Attack:
                _cdAttack = _maxAttack;
                SetActive(bgcooldown_attack, true);
                if (cdTextAttack) cdTextAttack.text = Mathf.CeilToInt(_cdAttack).ToString();
                break;
        }
    }

    private void TickCooldownUI()
    {
        TickOne(ref _cdTeleport, bgcooldown_teleport, cdTextTeleport);
        TickOne(ref _cdHide, bgcooldown_hide, cdTextHide);
        TickOne(ref _cdScream, bgcooldown_scream, cdTextScream);
        TickOne(ref _cdSpecial, bgcooldown_special, cdTextSpecial);
        TickOne(ref _cdAttack, bgcooldown_attack, cdTextAttack);
    }

    private void TickOne(ref float timer, GameObject bg, TextMeshProUGUI txt)
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = 0f;
            SetActive(bg, false);
            return;
        }
        if (txt != null) txt.text = Mathf.CeilToInt(timer).ToString();
    }

    public bool IsCooldown(SkillType skill) => skill switch
    {
        SkillType.Teleport => _cdTeleport > 0f,
        SkillType.Hide => _cdHide > 0f,
        SkillType.Scream => _cdScream > 0f,
        SkillType.Special => _cdSpecial > 0f,
        SkillType.Attack => _cdAttack > 0f,
        _ => false
    };

    public void SetRecipeDisplay(GhostType ghostType)
    {
        _myGhostTypeEnum = ghostType;
        if (recipePanel != null) return;

        if (GamePhaseManager.Instance != null &&
            GamePhaseManager.Instance.currentPhase.Value >= 2)
            ShowRecipeForPhase2();
    }

    public void ShowRecipeForPhase2()
    {
        var registry = GameRecipeRegistry.Instance;
        if (registry == null || recipePanel == null) return;

        var items = registry.GetRecipeFor(_myGhostTypeEnum);
        if (items.Length == 0) return;

        string text = "Items to protect:\n";
        foreach (var item in items) text += $"- {item}\n";

        if (recipeText != null) recipeText.text = text.TrimEnd();
    }

    public void ToggleControls()
    {
        if (controlsPanel == null) return;
        bool next = !controlsPanel.activeSelf;
        controlsPanel.SetActive(next);
        Cursor.lockState = next ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = next;
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

    public void OnClickNamaGhost()
    {
        if (_activeInfoPanel == null) return;
        _infoVisible = !_infoVisible;
        _activeInfoPanel.SetActive(_infoVisible);
    }

    public void UpdateTime(string t)
    {
        if (timeText != null) timeText.text = t;
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

    public void ShowResult(bool ghostWins)
    {
        SetActive(popupGhostWin, ghostWins);
        SetActive(popupGhostLose, !ghostWins);
        HUDManager.Instance?.OnGameEnded();
    }

    public void OpenMessagePanel(string teks)
    {
        if (MessagePanel == null || MessageText == null) return;
        MessageText.text = teks;
        MessagePanel.SetActive(true);
    }

    public void CloseMessagePanel() => SetActive(MessagePanel, false);

    public void ShowHoldProgress(float progress)
    {
        if (holdProgressObj != null) holdProgressObj.SetActive(true);
        if (holdProgressSlider != null) holdProgressSlider.value = progress;
    }

    public void HideHoldProgress()
    {
        if (holdProgressObj != null) holdProgressObj.SetActive(false);
        if (holdProgressSlider != null) holdProgressSlider.value = 0f;
    }

    public void OnRitualProgressChangedPublic(float v)
    {
        if (ritualBar != null) ritualBar.value = v;
    }

    public void UpdateTimePublic(string t) => UpdateTime(t);
    public void UpdatePhasePublic(int phase) => UpdatePhase(phase);
    public void ShowRitualBar() => SetActive(ritualBarObj, true);
    public void HideRitualBar() => SetActive(ritualBarObj, false);

    private void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}