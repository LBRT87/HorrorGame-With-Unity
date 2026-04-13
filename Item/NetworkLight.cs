using Unity.Netcode;
using UnityEngine;

public class NetworkLight : NetworkBehaviour
{
    private Light _light;

    private NetworkVariable<bool> _isOn = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        _light = GetComponentInChildren<Light>();
        if (_light == null)
            Debug.LogError($"[NetworkLight] Light component ganemu");
    }

    public override void OnNetworkSpawn()
    {
        _isOn.OnValueChanged += (_, val) =>
        {
            ApplyLight(val);
            Debug.Log($"[NetworkLight] {gameObject.name} jadi {(val ? "ON" : "OFF")}");
        };
        ApplyLight(_isOn.Value);
        Debug.Log($"[NetworkLight] {gameObject.name} spawned, state={_isOn.Value}");
    }

    private void ApplyLight(bool on)
    {
        if (_light != null)
        {
            _light.enabled = on;
            _light.intensity = on ? 1f : 0f;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleLightServerRpc()
    {
        _isOn.Value = !_isOn.Value;
        Debug.Log($"[NetworkLight] ServerRpc: toggle {_isOn.Value}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetLightServerRpc(bool on)
    {
        _isOn.Value = on;
    }

    public bool IsOn => _isOn.Value;
}