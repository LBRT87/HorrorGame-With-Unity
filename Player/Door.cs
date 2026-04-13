using Unity.Netcode;
using UnityEngine;

public class Door : NetworkBehaviour
{
    public NetworkVariable<bool> isOpen = new(false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 3f;
    [SerializeField] private bool isLocked = false;

    private Quaternion _closedRot;
    private Quaternion _openRot;

    private void Awake()
    {
        _closedRot = transform.rotation;
        _openRot = Quaternion.Euler(transform.eulerAngles + Vector3.up * openAngle);
    }

    public override void OnNetworkSpawn()
    {
        isOpen.OnValueChanged += (_, open) =>
        {
            StopAllCoroutines();
            StartCoroutine(AnimateDoor(open ? _openRot : _closedRot));
        };
    }

    private void Update()
    {
    }

    private System.Collections.IEnumerator AnimateDoor(Quaternion target)
    {
        while (Quaternion.Angle(transform.rotation, target) > 0.5f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation, target, openSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = target;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ToggleDoorServerRpc()
    {
        if (isLocked) return;
        isOpen.Value = !isOpen.Value;
    }
}