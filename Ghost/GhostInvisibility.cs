using Unity.Netcode;
using UnityEngine;

public class GhostVisibilityManager : NetworkBehaviour
{
    [Header("Renderers to control")]
    public Renderer[] ghostRenderers;

    public NetworkVariable<bool> isVisible = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool _viewerIsGhost = false;   
    private bool _isAIGhost = false; 

    public override void OnNetworkSpawn()
    {
        _isAIGhost = (OwnerClientId == NetworkManager.ServerClientId);

        _viewerIsGhost = CheckViewerIsPlayerGhost();

        isVisible.OnValueChanged += OnVisibilityChanged;
        ApplyVisibility(isVisible.Value);

        if (IsServer && _isAIGhost)
            isVisible.Value = false;
    }

    public override void OnNetworkDespawn()
    {
        isVisible.OnValueChanged -= OnVisibilityChanged;
    }

    private void OnVisibilityChanged(bool _, bool next)
    {
        ApplyVisibility(next);
    }

    private void ApplyVisibility(bool visible)
    {
        bool shouldShow;

        if (_viewerIsGhost)
        {
            shouldShow = true;
        }
        else if (IsOwner && !_isAIGhost)
        {
            shouldShow = true;
        }
        else
        {
            shouldShow = visible;
        }

        foreach (var r in ghostRenderers)
            if (r != null) r.enabled = shouldShow;

        SetNameTagVisible(shouldShow || _viewerIsGhost);
    }

    public void SetVisible(bool value)
    {
        if (!IsOwner) return;
        SetVisibleServerRpc(value);
    }

    public void SetVisibleServer(bool value)
    {
        if (!IsServer) return;
        isVisible.Value = value;
    }

    public void ToggleAppear()
    {
        if (!IsOwner) return;
        SetVisibleServerRpc(!isVisible.Value);
    }

    public bool IsCurrentlyVisible => isVisible.Value;


    [ServerRpc]
    private void SetVisibleServerRpc(bool value)
    {
        isVisible.Value = value;
    }

    private bool CheckViewerIsPlayerGhost()
    {
        foreach (var g in FindObjectsByType<GhostBasic>(FindObjectsSortMode.None))
        {
            if (g.IsOwner && g.GetComponent<GhostAIBase>() == null)
                return true;
        }
        return false;
    }
    private void SetNameTagVisible(bool visible)
    {
        foreach (var c in GetComponentsInChildren<Canvas>(true))
            c.enabled = visible;

        foreach (var t in GetComponentsInChildren<TMPro.TextMeshPro>(true))
            t.enabled = visible;
    }
}