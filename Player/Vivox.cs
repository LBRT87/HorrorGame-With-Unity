using System.Collections;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class VivoxManager : MonoBehaviour
{
    public static VivoxManager Instance;

    private HashSet<string> _activeChannels = new HashSet<string>();
    private bool isInitialized = false;
    private bool _isMuted = false;
    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            await InitVivox();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async System.Threading.Tasks.Task InitVivox()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        await VivoxService.Instance.InitializeAsync();

        Debug.Log($"[Vivox] Logged in as: {AuthenticationService.Instance.PlayerId}");
        isInitialized = true;
    }

    public async void JoinChannel(string channelName)
    {
        if (!isInitialized || string.IsNullOrEmpty(channelName)) return;
        if (_activeChannels.Contains(channelName)) return;

        await VivoxService.Instance.JoinPositionalChannelAsync(
            channelName,
            ChatCapability.AudioOnly,
            new Channel3DProperties(32, 1, 1, AudioFadeModel.InverseByDistance)
        );
        _activeChannels.Add(channelName);
        Debug.Log($"[Vivox] Joined: {channelName}");
    }

    public void Update3DPosition(Vector3 pos, Vector3 forward, Vector3 up)
    {
        if (!isInitialized) return;
        foreach (var ch in _activeChannels)
            VivoxService.Instance.Set3DPosition(pos, pos, forward, up, ch, false);
    }

    public void SetMute(bool mute)
    {
        if (!isInitialized) return;
        _isMuted = mute;

        if (mute)
            VivoxService.Instance.MuteInputDevice();
        else
            VivoxService.Instance.UnmuteInputDevice();
    
        Debug.Log($"[Vivox] Mute: {mute}");
    }
    public async void LeaveChannel(string channelName)
    {
        if (!isInitialized || string.IsNullOrEmpty(channelName)) return;
        if (!_activeChannels.Contains(channelName)) return;

        await VivoxService.Instance.LeaveChannelAsync(channelName);
        _activeChannels.Remove(channelName);
        Debug.Log($"[Vivox] Left: {channelName}");
    }
    public async void LeaveAllChannels()
    {
        if (!isInitialized) return;
        foreach (var ch in new HashSet<string>(_activeChannels))
            await VivoxService.Instance.LeaveChannelAsync(ch);
        _activeChannels.Clear();
        Debug.Log("[Vivox] Left all channels");
    }
    public bool IsInChannel(string channelName) => _activeChannels.Contains(channelName);
    public bool IsMuted => _isMuted;

}