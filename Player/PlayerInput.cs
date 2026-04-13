using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : NetworkBehaviour
{
    public Vector2 inputmove { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool IsRun { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool IsSwimming { get; private set; }
    public bool PickPressed { get; private set; }
    public bool ThrowPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool Slot1Pressed { get; private set; }
    public bool Slot2Pressed { get; private set; }
    public bool SettingsPressed { get; private set; }
    public bool TorchPressed { get; private set; }
    public bool IsHealHeld { get; private set; }
    public bool HealPressed { get; private set; }
    public Vector2 look { get; private set; }
    public bool SpecialItemPressed { get; private set; }
    public bool IsControls { get; private set; }

    public CameraChanger camerachanger;
    public bool IsCrouchHeld { get; private set; }

    private PlayerInput _playerInput;
    public HUD _hud;
    private bool _inputBound = false;
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerInput] Spawn — IsOwner: {IsOwner}, IsClient: {IsClient}");
        _playerInput = GetComponent<PlayerInput>();

        if (!IsOwner)
        {
            if (_playerInput != null)
                _playerInput.enabled = false;
            return;
        }

        if (_playerInput != null)
            _playerInput.enabled = true;

        try
        {
            BindAllActions();
            _inputBound = true;
            Debug.Log("[PlayerInputHandler] Bind sukses langsung");
        }
        catch
        {
            StartCoroutine(BindInputWithRetry());
        }
    }

    private IEnumerator BindInputWithRetry()
    {
        int tries = 0;
        while (!_inputBound && tries < 10) 
        {
            if (_playerInput == null)
            {
                _playerInput = GetComponent<PlayerInput>();
                if (_playerInput == null)
                {
                    Debug.LogError("[InputHandler] PlayerInput null");
                    yield return new WaitForSeconds(0.3f);
                    tries++;
                    continue;
                }
            }

            bool success = false;
            try
            {
                BindAllActions();
                success = true;
                _inputBound = true;
                Debug.Log($"[InputHandler] Input bound  untuk client {OwnerClientId}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[InputHandler] Bind gagal: {e.Message}");
            }

            if (!success)
            {
                yield return new WaitForSeconds(0.2f);
                tries++;
            }
        }

        if (!_inputBound)
            Debug.LogError($"[InputHandler] GAGAL bind ");
    }

    private void BindAllActions()
    {
        var actions = _playerInput.actions;

        actions["Move"].performed += ctx => inputmove = ctx.ReadValue<Vector2>();
        actions["Move"].canceled += ctx => inputmove = Vector2.zero;

        actions["Look"].performed += ctx => look = ctx.ReadValue<Vector2>();
        actions["Look"].canceled += ctx => look = Vector2.zero;

        actions["Jump"].started += ctx => JumpPressed = true;
        actions["Run"].started += ctx => IsRun = true;
        actions["Run"].canceled += ctx => IsRun = false;
        actions["PickUp"].started += ctx => PickPressed = true;
        actions["ThrowItem"].started += ctx => ThrowPressed = true;
        actions["Interact"].started += ctx => InteractPressed = true;
        actions["Slot1"].started += ctx => Slot1Pressed = true;
        actions["Slot2"].started += ctx => Slot2Pressed = true;
        actions["Torch"].started += ctx => TorchPressed = true;
        actions["SpecialItem"].started += ctx =>
        {
            SpecialItemPressed = true;
            Debug.Log("[InputHandler] special item pressed");
        };

        actions["Heal"].started += ctx => { IsHealHeld = true; HealPressed = true; };
        actions["Heal"].canceled += ctx => { IsHealHeld = false; HealPressed = false; };

        actions["Crouch"].started += ctx => IsCrouchHeld = true;
        actions["Crouch"].canceled += ctx => IsCrouchHeld = false;

        actions["SwitchCamera"].performed += ctx =>
        {
            if (camerachanger != null) camerachanger.gantiMode();
        };

        actions["Settings"].started += ctx =>
        {
            if (SettingsLogicOnGame.instance != null)
                SettingsLogicOnGame.instance.ToggleSettings();
        };

        actions["ShowControlsUI"].started += ctx =>
        {
            if (_hud == null) _hud = HUDManager.Instance?.GetExorcistHUD();
            if (_hud != null)
                _hud.OpenControlsUI();
            else
                Debug.LogWarning("[InputHandler] control mslh");

            if (_playerInput != null) _playerInput.enabled = true;
        };
    }

    public override void OnNetworkDespawn()
    {
        if (_playerInput != null)
            _playerInput.enabled = false;
    }

    public void ResetJump() { JumpPressed = false; }
    public void ResetPick() { PickPressed = false; }
    public void ResetThrow() { ThrowPressed = false; }
    public void ResetInteract() { InteractPressed = false; }
    public void ResetSlot1() { Slot1Pressed = false; }
    public void ResetSlot2() { Slot2Pressed = false; }
    public void ResetSettings() { SettingsPressed = false; }
    public void ResetTorch() { TorchPressed = false; }
    public void ResetHealPressed() { HealPressed = false; }
    public void ResetSpecialItem() { SpecialItemPressed = false; }
    public void SetClimbing(bool value) { IsClimbing = value; }
    public void SetSwimming(bool value) { IsSwimming = value; }
    public void ResetCrouch() { IsCrouchHeld = false; }
    public void ResetControls() { IsControls = false; }
}