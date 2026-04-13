using System;
using Unity.Netcode;
using UnityEngine;

public class GamePhaseManager : NetworkBehaviour
{
    public static GamePhaseManager Instance;

    private const float GAME_MINUTES_PER_REAL_SECOND = 0.5f;
    private const float START_GAME_MINUTE = 23 * 60f;  
    private const float END_GAME_MINUTE = 29 * 60f; 
    private const float PHASE2_MINUTE = 25 * 60f;   
    private const float PHASE3_MINUTE = 27 * 60f;   
    private const float LASTMINUTE_MINUTE = 28.5f * 60f;

    public NetworkVariable<float> currentGameMinute = new NetworkVariable<float>(
        START_GAME_MINUTE,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> currentPhase = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public static event Action<string> OnPhaseChanged;
    public static event Action<float> OnTimeChanged;
    public static event Action OnOneMinuteLeft;

    private bool phase2Triggered = false;
    private bool phase3Triggered = false;
    private bool lastMinuteTriggered = false;
    private bool gameOverTriggered = false;

    private float _syncTimer = 0f;
    private const float SYNC_INTERVAL = 2f;
    private float _localGameMinute = START_GAME_MINUTE;
    private bool _receivedFirstSync = false;

    [SerializeField] private AudioClip oneMinuteLeftClip;
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f;
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;

        currentPhase.OnValueChanged += (_, newVal) =>
        {
            OnPhaseChanged?.Invoke($"Phase {newVal}");
            HUDManager.Instance?.GetExorcistHUD()?.UpdatePhase(newVal);
            HUDManager.Instance?.GetGhostHUD()?.UpdatePhasePublic(newVal);
            Debug.Log($"[GPM] Phase  {newVal}");
        };

        if (!IsServer)
            RequestSyncServerRpc();

        if (currentPhase.Value >= 4)
            ShowCountdownClientRpc();

        Debug.Log($"[GPM] Spawn: time={currentGameMinute.Value}, phase={currentPhase.Value}");
    }

    private void Update()
    {
        if (IsServer) ServerUpdate();
        else ClientUpdate();
    }

    private void ServerUpdate()
    {
        if (gameOverTriggered) return;

        currentGameMinute.Value += GAME_MINUTES_PER_REAL_SECOND * Time.deltaTime;
        float t = currentGameMinute.Value;

        if (!phase2Triggered && t >= PHASE2_MINUTE)
        {
            phase2Triggered = true;
            currentPhase.Value = 2;
            Debug.Log("[GPM] Phase 2 01:00 AM");
        }

        if (!phase3Triggered && t >= PHASE3_MINUTE)
        {
            phase3Triggered = true;
            currentPhase.Value = 3;
            Debug.Log("[GPM] Phase 3 03:00 AM");
        }

        if (!lastMinuteTriggered && t >= LASTMINUTE_MINUTE)
        {
            lastMinuteTriggered = true;
            currentPhase.Value = 4;
            ShowCountdownClientRpc();
            RitualManager.Instance?.NotifyOneMinuteLeft();
            OnOneMinuteLeft?.Invoke();
            PlayOneMinuteLeftSFXClientRpc();
            Debug.Log("[GPM] Last Minute 04:30 AM");
        }

        if (t >= END_GAME_MINUTE)
        {
            gameOverTriggered = true;
            RitualManager.Instance?.OnTimeUp();
            ShowLoseClientRpc();
            Debug.Log("[GPM] Game Over 05:00 AM");
        }

        _syncTimer -= Time.deltaTime;
        if (_syncTimer <= 0f)
        {
            _syncTimer = SYNC_INTERVAL;
            SyncTimeToClientsClientRpc(currentGameMinute.Value);
        }

        PushTimeToHUD(t);
    }

    private void ClientUpdate()
    {
        if (!_receivedFirstSync) return;
        _localGameMinute += GAME_MINUTES_PER_REAL_SECOND * Time.deltaTime;
        PushTimeToHUD(_localGameMinute);
    }

    public void CheatSkipPhase()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            DoSkipPhase();
        else if (IsSpawned)
            CheatSkipPhaseServerRpc();
        else
            Debug.LogWarning("[GPM] CheatSkipPhase: belum spawned");
    }

    [ServerRpc(RequireOwnership = false)]
    public void CheatSkipPhaseServerRpc() => DoSkipPhase();

    private void DoSkipPhase()
    {
        if (!IsServer) return;
        gameOverTriggered = false;

        Debug.Log($"[DoSkipPhase] Dari Phase {currentPhase.Value}");

        switch (currentPhase.Value)
        {
            case 1:
                phase2Triggered = true;
                phase3Triggered = false;
                lastMinuteTriggered = false;
                currentPhase.Value = 2;
                currentGameMinute.Value = PHASE2_MINUTE + 0.01f;
                break;
            case 2:
                phase3Triggered = true;
                lastMinuteTriggered = false;
                currentPhase.Value = 3;
                currentGameMinute.Value = PHASE3_MINUTE + 0.01f;
                break;
            case 3:
                lastMinuteTriggered = true;
                currentPhase.Value = 4;
                currentGameMinute.Value = LASTMINUTE_MINUTE + 0.01f;
                ShowCountdownClientRpc();
                break;
            case 4:
                Debug.Log("[DoSkipPhase] Sudah phase 4, tidak ada yang dilakukan");
                return;
        }

        SyncTimeToClientsClientRpc(currentGameMinute.Value);
        Debug.Log($"[Cheat] Waktu {FormatTime(currentGameMinute.Value)}");
    }

    public void RitualPhaseUp()
    {
        if (!IsServer) return;
        int curr = currentPhase.Value;
        if (curr >= 4)
        {
            Debug.Log("[GPM] RitualPhaseUp: sudah phase 4, skip");
            return;
        }
        currentPhase.Value = curr + 1;
        Debug.Log($"[GPM] RitualPhaseUp: {curr} = {curr + 1}teetp");
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSyncServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong id = rpcParams.Receive.SenderClientId;
        SyncTimeToOneClientRpc(currentGameMinute.Value, currentPhase.Value,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                { TargetClientIds = new[] { id } }
            });
    }

    [ClientRpc]
    private void SyncTimeToOneClientRpc(float serverTime, int phase,
        ClientRpcParams _ = default)
    {
        _localGameMinute = serverTime;
        _receivedFirstSync = true;
        PushTimeToHUD(serverTime);
        HUDManager.Instance?.GetExorcistHUD()?.UpdatePhase(phase);
        HUDManager.Instance?.GetGhostHUD()?.UpdatePhasePublic(phase);
        Debug.Log($"[GPM] Latejoin sync: {FormatTime(serverTime)}, phase={phase}");
    }

    [ClientRpc]
    private void SyncTimeToClientsClientRpc(float serverTime)
    {
        if (IsServer) return;
        float diff = Mathf.Abs(serverTime - _localGameMinute);
        _localGameMinute = (!_receivedFirstSync || diff > 60f)
            ? serverTime
            : Mathf.Lerp(_localGameMinute, serverTime, 0.3f);
        _receivedFirstSync = true;
    }

    private void PushTimeToHUD(float minute)
    {
        string t = FormatTime(minute);
        HUDManager.Instance?.GetExorcistHUD()?.UpdateTime(t);
        HUDManager.Instance?.GetGhostHUD()?.UpdateTimePublic(t);
    }

    private void LateUpdate()
    {
        if (!lastMinuteTriggered || gameOverTriggered) return;
        float refTime = IsServer ? currentGameMinute.Value : _localGameMinute;
        float remaining = Mathf.Max(0,
            (END_GAME_MINUTE - refTime) / GAME_MINUTES_PER_REAL_SECOND);
        string txt = Mathf.CeilToInt(remaining).ToString();
        HUDManager.Instance?.GetExorcistHUD()?.UpdateCountdown(txt);
        HUDManager.Instance?.GetGhostHUD()?.UpdateCountdown(txt);
    }

    [ClientRpc]
    private void PlayOneMinuteLeftSFXClientRpc()
    {
        if (_audioSource != null && oneMinuteLeftClip != null)
            _audioSource.PlayOneShot(oneMinuteLeftClip);
    }

    [ClientRpc]
    private void ShowLoseClientRpc()
    {
        HUDManager.Instance?.GetExorcistHUD()?.ShowDeathPopup();
        HUDManager.Instance?.GetGhostHUD()?.ShowResult(true);
    }

    [ClientRpc]
    private void ShowCountdownClientRpc()
    {
        HUDManager.Instance?.GetExorcistHUD()?.ShowCountdown();
        HUDManager.Instance?.GetGhostHUD()?.ShowCountdown();
    }

    public string FormatTime(float gameMinute)
    {
        float norm = gameMinute % (24 * 60);
        int hours = (int)(norm / 60) % 24;
        int minutes = (int)(norm % 60);
        string ampm = hours >= 12 ? "AM" : "PM";
        int displayHour = hours % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour:00}:{minutes:00} {ampm}";
    }

    public float GetCurrentMinute()
        => IsServer ? currentGameMinute.Value : _localGameMinute;


}