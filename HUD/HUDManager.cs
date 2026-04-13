using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    public GameObject hudExorcistPrefab;
    public GameObject hudGhostPrefab;

    private HUD _exorcistHUD;
    private GhostHUD _ghostHUD;

    public GameObject hostDisconnectedPrefab;
    public GameObject hostDisconnectedInstance;
    private bool _subscribed = false;

    private void Awake()
    {
        if (Instance != null) { 
            Destroy(gameObject); 
            return; }
        Instance = this;
    }


    private bool _callbackRegistered = false;

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
    }
    
    public void SpawnHUDForRole(bool isGhost)
    {
        Transform mainCanvas = GameObject.FindWithTag("MainCanvasHUD")?.transform;
        if (mainCanvas == null)
        {
            Debug.LogError("[HUDManager] g ada Canvas tag MainCanvasHUD");
            return;
        }

        if (isGhost)
        {
            if (_ghostHUD == null && hudGhostPrefab != null)
            {
                GameObject go = Instantiate(hudGhostPrefab, mainCanvas);
                _ghostHUD = go.GetComponent<GhostHUD>();
                Debug.Log("[HUDManager] GhostHUD spawned");
            }
        }
        else
        {
            if (_exorcistHUD == null && hudExorcistPrefab != null)
            {
                GameObject go = Instantiate(hudExorcistPrefab, mainCanvas);
                _exorcistHUD = go.GetComponent<HUD>();
                Debug.Log("[HUDManager] ExorcistHUD spawned");
            }
        }

        if (!_subscribed)
        {
            _subscribed = true;
            StartCoroutine(SubscribeCoroutine());
        }
        else
        {
            StartCoroutine(ForcePushAfterFrame());
        }
    }

    public void RegisterExorcistHUD(HUD hud)
    {
        _exorcistHUD = hud;
        Debug.Log("[HUDManager] Exorcist HUD regis");

        ForcePushNow();
    }

    public void RegisterGhostHUD(GhostHUD hud)
    {
        _ghostHUD = hud;
        Debug.Log("[HUDManager] Ghost HUD regis");

        ForcePushNow();
    }
    public HUD GetExorcistHUD()
    {
        if (_exorcistHUD == null)
            _exorcistHUD = FindObjectOfType<HUD>();
        return _exorcistHUD;
    }

    public GhostHUD GetGhostHUD()
    {
        if (_ghostHUD == null)
            _ghostHUD = FindObjectOfType<GhostHUD>();
        return _ghostHUD;
    }

    private void ForcePushNow()
    {
        var gpm = GamePhaseManager.Instance;
        if (gpm == null) return;

        string t = FormatTime(gpm.GetCurrentMinute());
        int phase = gpm.currentPhase.Value;

        _exorcistHUD?.UpdateTime(t);
        _exorcistHUD?.UpdatePhase(phase);
        _ghostHUD?.UpdateTimePublic(t);
        _ghostHUD?.UpdatePhasePublic(phase);

        Debug.Log($"[HUDManager] ForcePush time={t}, phase={phase}");
    }

    private IEnumerator ForcePushAfterFrame()
    {
        yield return null;
        yield return null; 
        ForcePushNow();
    }

    private IEnumerator SubscribeCoroutine()
    {
        int tries = 0;
        while (GamePhaseManager.Instance == null && tries < 30)
        {
            yield return new WaitForSeconds(0.2f);
            tries++;
        }

        if (GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.currentGameMinute.OnValueChanged -= OnTimeChanged;
            GamePhaseManager.Instance.currentPhase.OnValueChanged -= OnPhaseChanged;

            GamePhaseManager.Instance.currentGameMinute.OnValueChanged += OnTimeChanged;
            GamePhaseManager.Instance.currentPhase.OnValueChanged += OnPhaseChanged;

            Debug.Log("[HUDManager] Subscribe ke GamePhaseManager berhasil");

            yield return null;
            yield return null;

            ForcePushNow();
        }
        else
        {
            Debug.LogError("[HUDManager] GamePhaseManager gada");
        }

        tries = 0;
        while (RitualManager.Instance == null && tries < 20)
        {
            yield return new WaitForSeconds(0.2f);
            tries++;
        }

        if (RitualManager.Instance != null)
        {
            RitualManager.Instance.ritualProgress.OnValueChanged -= OnRitualProgressChanged;
            RitualManager.Instance.ritualActive.OnValueChanged -= OnRitualActiveChanged;

            RitualManager.Instance.ritualProgress.OnValueChanged += OnRitualProgressChanged;
            RitualManager.Instance.ritualActive.OnValueChanged += OnRitualActiveChanged;

            OnRitualProgressChanged(0, RitualManager.Instance.ritualProgress.Value);
            OnRitualActiveChanged(false, RitualManager.Instance.ritualActive.Value);

            Debug.Log("[HUDManager] Subscribe ke RitualManager berhasil");
        }
    }

    private float _pushTimer = 0f;
    private const float PUSH_INTERVAL = 1f; 

    private void Update()
    {

        if (!_callbackRegistered && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            _callbackRegistered = true;
            Debug.Log("[HUDManager] OnClientDisconnect callback registered");
        }

        _pushTimer -= Time.deltaTime;
        if (_pushTimer > 0f) return;
        _pushTimer = PUSH_INTERVAL;

        var gpm = GamePhaseManager.Instance;
        if (gpm == null) return;
        if (_exorcistHUD == null && _ghostHUD == null) return;
        Debug.Log($"[HUDManager] Update push: {gpm.GetCurrentMinute()}");
        string t = FormatTime(gpm.GetCurrentMinute());
        int phase = gpm.currentPhase.Value;

        _exorcistHUD?.UpdateTime(t);
        _ghostHUD?.UpdateTimePublic(t);


            if (!_callbackRegistered && NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        _callbackRegistered = true;
        Debug.Log("[HUDManager] Disconnect callback registered");
    }
    }
    private void OnTimeChanged(float prev, float next)
    {
        string t = FormatTime(next);

        _exorcistHUD?.UpdateTime(t);
        _ghostHUD?.UpdateTimePublic(t);

        if (GamePhaseManager.Instance != null)
        {
            int phase = GamePhaseManager.Instance.currentPhase.Value;
            _exorcistHUD?.UpdatePhase(phase);
            _ghostHUD?.UpdatePhasePublic(phase);
        }
    }

    private void OnPhaseChanged(int prev, int next)
    {
        _exorcistHUD?.UpdatePhase(next);
        _ghostHUD?.UpdatePhasePublic(next);
        Debug.Log($"[HUDManager] Phase changed  {next}");
    }

    private void OnRitualProgressChanged(float prev, float next)
    {
        _exorcistHUD?.UpdateRitualProgress(next);
        _ghostHUD?.OnRitualProgressChangedPublic(next);
    }

    private void OnRitualActiveChanged(bool prev, bool next)
    {
        if (next)
        {
            _exorcistHUD?.ShowRitualBar();
            _ghostHUD?.ShowRitualBar();
        }
        else
        {
            _exorcistHUD?.HideRitualBar();
            _ghostHUD?.HideRitualBar();
        }
    }
    private void OnClientDisconnect(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer &&
            clientId == NetworkManager.ServerClientId)
        {
            Debug.Log("[HUDManager] Host disconnect detek");
            ShowHostDisconnectPopup();
        }
    }

    public void ShowHostDisconnectPopup()
    {
        Time.timeScale = 0f;
        GameObject canvasObj = GameObject.FindWithTag("MainCanvasHUD");
        if (canvasObj == null) return;

        if (hostDisconnectedPrefab != null && hostDisconnectedInstance == null)
        {
            hostDisconnectedInstance = Instantiate(hostDisconnectedPrefab, canvasObj.transform);
            hostDisconnectedInstance.SetActive(true);
        }
    }

    public void OnGameEnded()
    {
        Time.timeScale = 0f;
        Debug.Log("[HUDManager] Game ended, time paused.");
    }

    public void ResumeAndCleanup()
    {
        Time.timeScale = 1f;
    }

    private string FormatTime(float gameMinute)
    {
        float norm = gameMinute % (24 * 60);
        int h = (int)(norm / 60) % 24;
        int m = (int)(norm % 60);
        string ampm = h >= 12 ? "AM" : "PM";

        int dh = h % 12;

        if (dh == 0) dh = 12;
        return $"{dh:00}:{m:00} {ampm}";
    }
}