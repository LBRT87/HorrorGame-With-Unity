using System.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class GhostInputHandler : NetworkBehaviour
{
    public CameraChanger cameraChanger;
    public Vector2 InputMove { get; private set; }
    public Vector2 InputLook { get; private set; }
    public bool InvisibilityPressed { get; private set; }
    public bool TeleportPressed { get; private set; }
    public bool ScreamPressed { get; private set; }
    public bool SpecialSkillPressed { get; private set; }
    public bool IsSpecialHeld { get; private set; }
    public bool FlyPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool AttackClickedMouse { get; private set; }
    public bool AttackModePressed { get; private set; }
    public bool AttackModeHeld { get; private set; }

    private PlayerInput _playerInput;
    private bool _inputBound = false;
    private InputAction moveAction;
    private InputAction flyAction;

    public override void OnNetworkSpawn()
    {
        _playerInput = GetComponent<PlayerInput>();

        if (!IsOwner)
        {
            if (_playerInput != null) _playerInput.enabled = false;
            return;
        }

        if (_playerInput != null) _playerInput.enabled = true;

        try
        {
            BindAllActions();
            _inputBound = true;
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
                    yield return new WaitForSeconds(0.3f);
                    tries++;
                    continue;
                }
            }

            bool success = false;
            try { BindAllActions(); success = true; _inputBound = true; }
            catch { }

            if (!success) { yield return new WaitForSeconds(0.2f); tries++; }
        }

        if (!_inputBound) Debug.LogError("[GhostInputHandler] Gagal bind input");
    }

    private void BindAllActions()
    {
        if (cameraChanger == null)
            cameraChanger = GetComponentInChildren<CameraChanger>(true) ?? GetComponent<CameraChanger>();

        _playerInput.SwitchCurrentActionMap("MultiPlayerGhostControl");
        var actions = _playerInput.actions;

        moveAction = actions["Move"];
        moveAction.performed += OnMove;
        moveAction.canceled += OnMoveCancel;

        flyAction = actions["Fly"];
        flyAction.started += OnFly;

        actions["Look"].performed += ctx => InputLook = ctx.ReadValue<Vector2>();
        actions["Look"].canceled += ctx => InputLook = Vector2.zero;

        actions["ToggleInvisibility"].started += _ => InvisibilityPressed = true;
        actions["Teleport"].started += _ => TeleportPressed = true;
        actions["Interact"].started += _ => InteractPressed = true;
        actions["Fly"].started += _ => FlyPressed = true;
        actions["MakeSound"].started += _ => ScreamPressed = true;
        actions["SpecialSkill"].started += _ => SpecialSkillPressed = true;
        actions["SpecialSkill"].performed += _ => IsSpecialHeld = true;
        actions["SpecialSkill"].canceled += _ => IsSpecialHeld = false;
        actions["AttackMode"].started += _ => AttackModePressed = true;
        actions["AttackMode"].performed += _ => AttackModeHeld = true;
        actions["AttackMode"].canceled += _ => AttackModeHeld = false;
        actions["Attack"].started += _ => AttackClickedMouse = true;

        actions["SwitchCamera"].performed += _ =>
        {
            if (cameraChanger != null)
                cameraChanger.gantiMode();
            else
                Debug.LogWarning("[GhostInputHandler] CameraChanger null saat SwitchCamera");
        };

        actions["Settings"].started += _ =>
        {
            if (SettingsLogicOnGame.instance != null)
                SettingsLogicOnGame.instance.ToggleSettings();
            else
                Debug.LogWarning("[GhostInputHandler] SettingsLogicOnGame NULL");
        };

        actions["ShowControlsUI"].started += _ =>
        {
            FindFirstObjectByType<GhostHUD>()?.ToggleControls();
        };
    }

    public override void OnNetworkDespawn()
    {
        if (moveAction != null) { moveAction.performed -= OnMove; moveAction.canceled -= OnMoveCancel; }
        if (flyAction != null) flyAction.started -= OnFly;
    }

    public void ResetInvisibility() => InvisibilityPressed = false;
    public void ResetTeleport() => TeleportPressed = false;
    public void ResetScream() => ScreamPressed = false;
    public void ResetSpecialSkill() => SpecialSkillPressed = false;
    public void ResetAttackClick() => AttackClickedMouse = false;
    public void ResetInteract() => InteractPressed = false;
    public void ResetFly() => FlyPressed = false;
    public void ResetAttackMode() => AttackModePressed = false;

    private void OnMove(InputAction.CallbackContext ctx) => InputMove = ctx.ReadValue<Vector2>();
    private void OnMoveCancel(InputAction.CallbackContext ctx) => InputMove = Vector2.zero;
    private void OnFly(InputAction.CallbackContext ctx) => FlyPressed = true;
}