using Unity.Netcode;
using UnityEngine;

public class PlayerAnimationSync : NetworkBehaviour
{
    private Animator _animator;

    private NetworkVariable<float> _netSpeedX = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<float> _netSpeedZ = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _netSwim = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _netClimb = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _netCrouch = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> _netLying = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) return;

        _netSpeedX.OnValueChanged += (_, v) => SetFloat("SpeedX", v);
        _netSpeedZ.OnValueChanged += (_, v) => SetFloat("SpeedZ", v);
        _netSwim.OnValueChanged += (_, v) => SetBool("isSwim", v);
        _netClimb.OnValueChanged += (_, v) => SetBool("isClimb", v);
        _netCrouch.OnValueChanged += (_, v) =>
        {
            SetBool("isCrouch", v);
            Debug.Log($"[AnimSync] isCrouch → {v}");
        };
        _netLying.OnValueChanged += (_, v) =>
        {
            SetBool("isLying", v);
            if (v)
            {
                SetBool("isMoving", false);

            }
        };
    }

    public void SyncMovementParams(float sx, float sz, float speed, bool swim, bool climb)
    {
        if (!IsOwner) return;
        _netSpeedX.Value = sx;
        _netSpeedZ.Value = sz;
        _netSwim.Value = swim;
        _netClimb.Value = climb;
    }

    public void SetCrouchSync(bool crouching)
    {
        if (!IsOwner) return;
        _netCrouch.Value = crouching;
        SetBool("isCrouch", crouching);
    }

    public void SetLyingSync(bool lying)
    {
        if (!IsOwner) return;
        _netLying.Value = lying;
        SetBool("isLying", lying);
        if (lying) SetBool("isMoving", false);
    }

    private void SetFloat(string param, float val)
    {
        if (_animator != null) _animator.SetFloat(param, val);
    }

    private void SetBool(string param, bool val)
    {
        if (_animator != null) _animator.SetBool(param, val);
    }
}