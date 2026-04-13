using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RitualManager : NetworkBehaviour
{
    public static RitualManager Instance;

    public Plate[] plates;
    public GameObject ritualProgressBarObj;
    public Slider ritualSlider;

    public NetworkVariable<float> ritualProgress = new(0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> ritualActive = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool _cheatOverride = false;
    private const float RITUAL_DURATION = 60f;

    [Header("Audio")]
    public AudioClip ritualClip;
    private AudioSource _audioSource;

    public static event Action<float> OnRitualProgressChanged;
    private bool _ritualPhaseUpgraded = false;

    private float _startupDelay = 2f;
    private bool _readyToCheck = false;

    private void Awake()
    {
        Instance = this;
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.clip = ritualClip;
    }

    public override void OnNetworkSpawn()
    {
        ritualProgress.OnValueChanged += (_, v) =>
        {
            if (ritualSlider != null) ritualSlider.value = v;
            HUDManager.Instance?.GetExorcistHUD()?.UpdateRitualProgress(v);
            HUDManager.Instance?.GetGhostHUD()?.OnRitualProgressChangedPublic(v);
        };

        ritualActive.OnValueChanged += (_, active) =>
        {
            if (active)
            {
                if (_audioSource != null && ritualClip != null && !_audioSource.isPlaying)
                    _audioSource.Play();
                HUDManager.Instance?.GetExorcistHUD()?.ShowRitualBar();
                HUDManager.Instance?.GetGhostHUD()?.ShowRitualBar();
            }
            else
            {
                if (_audioSource != null) _audioSource.Pause();
                HUDManager.Instance?.GetExorcistHUD()?.HideRitualBar();
                HUDManager.Instance?.GetGhostHUD()?.HideRitualBar();
            }
        };
    }

    void Update()
    {
        if (!IsServer) return;

        if (!_readyToCheck)
        {
            _startupDelay -= Time.deltaTime;
            if (_startupDelay <= 0f)
            {
                _readyToCheck = true;
                Debug.Log("[Ritual] Siap cek plates");
            }
            return;
        }

        bool platesOk = _cheatOverride || AllPlatesCorrect();

        if (!ritualActive.Value)
        {
            if (platesOk)
            {
                Debug.Log("[Ritual] start");
                StartRitual();
            }
        }
        else
        {
            if (!platesOk)
            {
                Debug.Log("[Ritual] pause item terganggu");
                PauseRitual();
            }
            else
            {
                ritualProgress.Value += Time.deltaTime / RITUAL_DURATION;
                OnRitualProgressChanged?.Invoke(ritualProgress.Value);

                if (ritualProgress.Value >= 1f)
                {
                    Debug.Log("[Ritual] complete");
                    ritualProgress.Value = 1f;
                    ritualActive.Value = false;
                    _cheatOverride = false;
                    ShowWinClientRpc();
                }
            }
        }
    }

    private void StartRitual()
    {
        ritualActive.Value = true;
        ResumeRitualAudioClientRpc();
        ShowRitualBarClientRpc();

        if (!_ritualPhaseUpgraded && GamePhaseManager.Instance != null)
        {
            GamePhaseManager.Instance.RitualPhaseUp();
            _ritualPhaseUpgraded = true;
        }
    }

    private void PauseRitual()
    {
        ritualActive.Value = false;
        PauseRitualAudioClientRpc();
    }

    private void StopRitual()
    {
        ritualActive.Value = false;
        ritualProgress.Value = 0f;
        _ritualPhaseUpgraded = false; 
        _cheatOverride = false;
        StopRitualAudioClientRpc();
        HideRitualBarClientRpc();
    }

    private bool AllPlatesCorrect()
    {
        if (plates == null || plates.Length == 0) return false;

        var registry = GameRecipeRegistry.Instance;
        if (registry == null) return false;

        var required = registry.GetAllRequiredItems();

        if (required == null || required.Count == 0) return false;

        var onPlate = new List<ItemType>();
        foreach (var plate in plates)
        {
            if (plate == null || plate.IsEmpty()) continue;
            var item = plate.GetCurrentItem();
            if (item != null) onPlate.Add(item.itemType);
        }

        if (onPlate.Count < required.Count) return false;

        foreach (var reqItem in required)
        {
            if (!onPlate.Contains(reqItem)) return false;
        }

        Debug.Log("[Ritual] Semua item benar di plate!");
        return true;
    }

    public void CheatForceStartRitual()
    {
        if (!IsServer) { CheatForceStartServerRpc(); return; }
        DoCheatStart();
    }

    [ServerRpc(RequireOwnership = false)]
    private void CheatForceStartServerRpc() => DoCheatStart();

    private void DoCheatStart()
    {
        Debug.Log("[RitualManager] Cheat: paksa start ritual");
        _cheatOverride = true;
        _ritualPhaseUpgraded = false; 
        ritualActive.Value = true;
        ritualProgress.Value = 0f;
        PlayRitualAudioClientRpc();
        ShowRitualBarClientRpc();
    }

    [ClientRpc]
    private void ResumeRitualAudioClientRpc()
    {
        if (_audioSource == null || ritualClip == null) return;
        if (!_audioSource.isPlaying)
        {
            if (_audioSource.time > 0f) _audioSource.UnPause();
            else _audioSource.Play();
        }
    }

    [ClientRpc]
    private void PauseRitualAudioClientRpc()
    {
        if (_audioSource != null && _audioSource.isPlaying) _audioSource.Pause();
    }

    [ClientRpc]
    private void PlayRitualAudioClientRpc()
    {
        if (_audioSource != null && ritualClip != null && !_audioSource.isPlaying)
            _audioSource.Play();
    }

    [ClientRpc]
    private void StopRitualAudioClientRpc()
    {
        if (_audioSource != null) _audioSource.Stop();
    }

    [ClientRpc]
    private void ShowRitualBarClientRpc()
    {
        HUDManager.Instance?.GetExorcistHUD()?.ShowRitualBar();
        HUDManager.Instance?.GetGhostHUD()?.ShowRitualBar();
    }

    [ClientRpc]
    private void HideRitualBarClientRpc()
    {
        HUDManager.Instance?.GetExorcistHUD()?.HideRitualBar();
        HUDManager.Instance?.GetGhostHUD()?.HideRitualBar();
    }

    [ClientRpc]
    private void ShowWinClientRpc()
    {
        HUDManager.Instance?.GetExorcistHUD()?.ShowWinPopup();
        HUDManager.Instance?.GetGhostHUD()?.ShowResult(false);
    }

    public void NotifyOneMinuteLeft() => Debug.Log("[Ritual] Sisa 1 menit");

    public void OnTimeUp()
    {
        Debug.Log("[Ritual] Waktu habis!");
        if (IsServer) StopRitual();
    }
}