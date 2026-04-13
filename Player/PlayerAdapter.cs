using UnityEngine;
using Unity.Netcode;

public class PlayerVoiceAdapter : NetworkBehaviour
{
    [Header("DEBUG")]
    public bool debugOverrideChannel = false;
    public string debugChannelName = "ghost-channel";

    private const string ExorcistChannel = "exorcist-channel";
    private const string GhostChannel = "ghost-channel";

    private PlayerRole _myRole;
    private bool _roleReady = false;
    private bool _crossChannelJoined = false;


    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        var pn = GetComponent<PlayerNetwork>();
        if (pn == null) return;

        if (pn.role.Value != PlayerRole.Exorcist && pn.role.Value != PlayerRole.Ghost)
        {
            pn.role.OnValueChanged += OnRoleAssigned;
            Debug.Log("[Voice] Waiting for role...");
        }
        else
        {
            _myRole = pn.role.Value;
            SetupVoice();
        }
    }

    private void OnRoleAssigned(PlayerRole oldRole, PlayerRole newRole)
    {
        var pn = GetComponent<PlayerNetwork>();
        if (pn != null) pn.role.OnValueChanged -= OnRoleAssigned;
        _myRole = newRole;
        Debug.Log($"[Voice] Role received: {_myRole}");
        SetupVoice();
    }

    public override void OnNetworkDespawn()
    {
        var pn = GetComponent<PlayerNetwork>();
        if (pn != null) pn.role.OnValueChanged -= OnRoleAssigned;

        if (VivoxManager.Instance != null)
            VivoxManager.Instance.LeaveAllChannels();
    }

    private void SetupVoice()
    {
        _roleReady = true;

        string channel = debugOverrideChannel
            ? debugChannelName
            : (_myRole == PlayerRole.Ghost ? GhostChannel : ExorcistChannel);

        VivoxManager.Instance?.JoinChannel(channel);
        Debug.Log($"[Voice] Auto-join '{channel}' sebagai {_myRole}");
    }


    private void Update()
    {
        if (!IsOwner || !_roleReady) return;
        if (VivoxManager.Instance == null) return;

        VivoxManager.Instance.Update3DPosition(
            transform.position,
            transform.forward,
            transform.up
        );
    }

    public bool IsVoiceActive => VivoxManager.Instance?.IsInChannel(
        _myRole == PlayerRole.Ghost ? GhostChannel : ExorcistChannel) ?? false;
    public bool IsCrossActive => _crossChannelJoined;

    public async void SetVoiceActive(bool active)
    {
        if (_myRole == PlayerRole.Ghost) return;

        if (active)
        {
            if (_crossChannelJoined) return;
            await System.Threading.Tasks.Task.Delay(200);
            string target = debugOverrideChannel ? debugChannelName : GhostChannel;
            VivoxManager.Instance?.JoinChannel(target);
            _crossChannelJoined = true;

            if (VivoxManager.Instance != null && VivoxManager.Instance.IsMuted)
                VivoxManager.Instance.SetMute(true);
        }
        else
        {
            if (!_crossChannelJoined) return;
            string target = debugOverrideChannel ? debugChannelName : GhostChannel;
            VivoxManager.Instance?.LeaveChannel(target);
            _crossChannelJoined = false;
        }
    }
}