using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerNameTag : NetworkBehaviour
{
    public TextMeshProUGUI nameTagText;

    private NetworkVariable<FixedString64Bytes> _syncedName =
        new NetworkVariable<FixedString64Bytes>(
            new FixedString64Bytes("Aleen"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        _syncedName.OnValueChanged += OnNameChanged;

        if (_syncedName.Value != "Aleen")
        {
            ApplyName(_syncedName.Value.ToString());
        }

        if (IsOwner)
        {
            StartCoroutine(InitNameRoutine());
        }
    }

    private void OnNameChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
    {
        Debug.Log($"[NameTag] Value changed from {oldVal} to {newVal}");
        ApplyName(newVal.ToString());
    }

    private IEnumerator InitNameRoutine()
    {
        while (saveMAnager.Instance == null) yield return null;

        yield return new WaitForEndOfFrame();

        string finalName = ResolveName();

        if (string.IsNullOrEmpty(finalName))
        {
            finalName = "Aleen";
               }

        SetNameServerRpc(finalName);
    }
    private string ResolveName()
    {
        if (saveMAnager.Instance != null)
        {
            return saveMAnager.Instance.GetUsernamePlayer();
        }
        return "";
    }

    [ServerRpc]
    public void SetNameServerRpc(string name)
    {
        _syncedName.Value = new FixedString64Bytes(name);
        ApplyName(name);
    }

    private void ApplyName(string name)
    {
        if (nameTagText != null) nameTagText.text = name;
    }

    public override void OnNetworkDespawn()
    {
        _syncedName.OnValueChanged -= OnNameChanged;
    }

    void LateUpdate()
    {
        if (nameTagText == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        nameTagText.transform.rotation = cam.transform.rotation;
    }
}