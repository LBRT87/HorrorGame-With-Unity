using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SpiritBoxInteraction : NetworkBehaviour
{
    [Header("UI — Mute button di Settings Panel")]
    [SerializeField] private Button muteButton;
    [SerializeField] private UnityEngine.UI.Image muteButtonIcon;

    [Header("Icons (opsional)")]
    [SerializeField] private Sprite iconMuted;
    [SerializeField] private Sprite iconUnmuted;

    private bool _isMuted = false;

    private void Start()
    {
        if (muteButton != null)
            muteButton.onClick.AddListener(ToggleMute);

        RefreshMuteUI();
    }

    public void ToggleMute()
    {
        _isMuted = !_isMuted;

        if (VivoxManager.Instance != null)
            VivoxManager.Instance.SetMute(_isMuted);

        RefreshMuteUI();
        Debug.Log($"[SpiritBox] Mute: {_isMuted}");
    }

    private void RefreshMuteUI()
    {
        if (muteButtonIcon == null) return;

        if (_isMuted && iconMuted != null)
            muteButtonIcon.sprite = iconMuted;
        else if (!_isMuted && iconUnmuted != null)
            muteButtonIcon.sprite = iconUnmuted;
    }

    public void SyncMuteState()
    {
        if (VivoxManager.Instance != null)
            _isMuted = VivoxManager.Instance.IsMuted;
        RefreshMuteUI();
    }
}