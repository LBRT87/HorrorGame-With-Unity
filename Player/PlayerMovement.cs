using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkAnimator))]
public class PlayerMovement : NetworkBehaviour
{
    public float speed = 4f;
    public float jump = 1.0f;
    public float gravuity = -10f;
    public float runMultiplier = 2f;
    public float climbSpped = 2f;
    public float swimSpeed = 3f;

    [SerializeField] public Transform Hand;
    public enum movingStatus { Walking, Climbing, Swimming }
    public movingStatus currMovingStatus = movingStatus.Walking;

    public PlayerInputHandler inputPlayerHandler;
    private CharacterController controller;
    private Animator animator;
    private Inventory _inventory;  
    private HealthSystem _health;
    private PlayerAnimationSync _animSync;
    public HUD Hud;
    [SerializeField] private CameraChanger cameraChanger;

    [Header("Detection")]
    [SerializeField] private LayerMask ground;
    [SerializeField] private LayerMask waterLayer;
    [SerializeField] private LayerMask ladder;
    [SerializeField] private Transform groundCheck;

    public bool isNyentuhTanah;
    private bool flagJump = false;
    private Vector3 velocity;
    private bool isInWater;
    private float waterSurfaceY;
    private bool isClimbing = false;
    private float waterTimer = 0f;
    private float waterGraceTime = 0.2f;
    private float _healHoldTimer = 0f;
    private const float HEAL_HOLD_DURATION = 4f;
    private bool _healUsed = false;

    [HideInInspector] public float sensitivity = 30f;
    public float crouchSpeedMultiplier = 0.5f;
    public float crouchHeight = 1f;
    private float _normalHeight;
    private bool _isCrouching = false;
    private ExorcistSoundEffects _soundFX;

    void Awake()
    {
        inputPlayerHandler = GetComponent<PlayerInputHandler>();
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        _inventory = GetComponent<Inventory>();  
        _health = GetComponent<HealthSystem>();
        _animSync = GetComponent<PlayerAnimationSync>();
        _soundFX = GetComponent<ExorcistSoundEffects>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            if (inputPlayerHandler != null)
                inputPlayerHandler.enabled = false;
            return;
        }

        if (cameraChanger != null) cameraChanger.InitCamera();

        if (controller != null) _normalHeight = controller.height;

        Hud = FindFirstObjectByType<HUD>();
        if (Hud != null) Hud.gameObject.SetActive(true);

        var ghostHud = FindFirstObjectByType<GhostHUD>();
        if (ghostHud != null) ghostHud.gameObject.SetActive(false);

        ApplyInitialSettings();
    }

    public void ApplyInitialSettings()
    {
        if (saveMAnager.Instance == null) return;
        sensitivity = saveMAnager.Instance.GetSensitivity();
        ApplyFOV(saveMAnager.Instance.GetFoV());
        AudioListener.volume = saveMAnager.Instance.GetVolume() / 100f;
    }

    public void ApplyFOV(float fovValue)
    {
        if (cameraChanger != null)
            cameraChanger.UpdateAllCamerasFOV(fovValue);
        else if (Camera.main != null)
            Camera.main.fieldOfView = fovValue;
    }

    private Transform GetCameraTransform()
    {
        if (cameraChanger != null) return cameraChanger.GetActiveCameraTransform();
        return Camera.main?.transform;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (NotebookChatManager.IsTyping)
        {
            return;
        }
        if (Hud == null) 
            Hud = FindFirstObjectByType<HUD>();
        if (_health == null) 
            _health = GetComponent<HealthSystem>();

        isNyentuhTanah = Physics.CheckSphere(groundCheck.position, 0.4f, ground);
        isInWater = Physics.CheckSphere(transform.position, 0.3f, waterLayer);

        if (isInWater) waterTimer = waterGraceTime;
        else waterTimer -= Time.deltaTime;

        bool nearLadder = Physics.CheckSphere(transform.position, 0.4f, ladder);

        if (nearLadder && !isClimbing)
        {
            if (_inventory == null || !_inventory.HasItemInHand())
            {
                isClimbing = true;
                velocity = Vector3.zero;
                Debug.Log("[Movement] Masuk ladder");
            }
        }

        if (isClimbing && !nearLadder)
        {
            Debug.Log("[Movement] Keluar ladder");
            SelesaiLadder();
        }

        if (isClimbing)
            currMovingStatus = movingStatus.Climbing;
        else if (waterTimer > 0f && !isNyentuhTanah)
            currMovingStatus = movingStatus.Swimming;
        else
            currMovingStatus = movingStatus.Walking;

        HandleStateExecution();
        HandleCrouch();
        UpdateAnimator();
    }
    private void HandleCrouch()
    {
        if (!IsOwner) return;
        bool wantCrouch = inputPlayerHandler.IsCrouchHeld;
        if (wantCrouch == _isCrouching) return;

        _isCrouching = wantCrouch;
        _soundFX?.SetCrouching(_isCrouching);

        if (controller != null)
            controller.height = _isCrouching ? crouchHeight : _normalHeight;

    }
    private void HandleStateExecution()
    {
        switch (currMovingStatus)
        {
            case movingStatus.Walking:
                HandleMovement();
                HandleJump();
                break;
            case movingStatus.Swimming:
                HandleSwimming();
                HandleBreathBar();
                break;
            case movingStatus.Climbing:
                HandleClimbing();
                break;
        }
        if (inputPlayerHandler.IsHealHeld)
            HandleHealHold();
        else
            ResetHealHold();
    }
    private void HandleHealHold()
    {
        if (_healUsed) return;

        if (_inventory != null && !_inventory.HasItemType(ItemType.Medkit))
        {
            ResetHealHold();
            return;
        }

        _healHoldTimer += Time.deltaTime;

        float progress = _healHoldTimer / HEAL_HOLD_DURATION;
        Hud?.ShowHealProgress(progress);

        if (_healHoldTimer >= HEAL_HOLD_DURATION)
        {
            _inventory?.TryUseMedkit();
            _healUsed = true;
            ResetHealHold();
            Hud?.HideHealProgress();
            Debug.Log("[Movement] Heal selesai");
        }
    }

    private void ResetHealHold()
    {
        _healHoldTimer = 0f;
        _healUsed = false;
        Hud?.HideHealProgress();
    }

    private void HandleMovement()
    {
        if (controller == null || !controller.enabled) return;
        Transform camTransform = GetCameraTransform();
        if (camTransform == null) return;

        Vector3 lookDir = camTransform.forward;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            float rotateSpeed = sensitivity > 0 ? sensitivity : 20f;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
        }

        Vector3 forward = camTransform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = camTransform.right; right.y = 0; right.Normalize();
        Vector3 moveDir = (forward * inputPlayerHandler.inputmove.y) + (right * inputPlayerHandler.inputmove.x);

        bool canRun = CanRun();
        float moveSpeed = _isCrouching ? speed * crouchSpeedMultiplier : (inputPlayerHandler.IsRun && canRun ? speed * runMultiplier : speed);

        if (isNyentuhTanah && velocity.y < 0) velocity.y = -2f;
        else velocity.y += gravuity * Time.deltaTime;

        Vector3 finalMotion = moveDir * moveSpeed;
        finalMotion.y = velocity.y;
        controller.Move(finalMotion * Time.deltaTime);
    }

    private bool CanRun()
    {
        if (_inventory != null && _inventory.HasItemInHand()) 
            return false;
        if (_health != null && _health.currentHealth.Value < 3)
            return false;
        return true;
    }

    private void HandleJump()
    {
        if (inputPlayerHandler.JumpPressed && isNyentuhTanah)
        {
            velocity.y = Mathf.Sqrt(jump * -2f * gravuity);
            flagJump = true;
            inputPlayerHandler.ResetJump();
            Debug.Log("[Movement] Jump");
        }
        if (isNyentuhTanah) flagJump = false;
    }

    private void HandleClimbing()
    {
        if (controller == null || !controller.enabled) return;
        if (_inventory != null && _inventory.HasItemInHand())
        {
            SelesaiLadder();
            return;
        }

        velocity = Vector3.zero;
        float vInput = inputPlayerHandler.inputmove.y;
        Vector3 climbDir = new Vector3(0, vInput * climbSpped, 0);
        controller.Move(climbDir * Time.deltaTime);

        if (isNyentuhTanah && vInput < -0.1f)
        {
            Debug.Log("[Movement] Selesai climbing ");
            SelesaiLadder();
            return;
        }

        bool adaLadderDiAtas = Physics.CheckSphere(
            transform.position + Vector3.up * 0.8f, 0.4f, ladder);

        if (vInput > 0.1f && !adaLadderDiAtas)
        {
            Debug.Log("[Movement] Selesai climbing");
            SelesaiLadder();
            Vector3 exitDir = transform.forward * 2f + Vector3.up * 0.5f;
            controller.Move(exitDir * Time.deltaTime * speed);
        }
    }

    private void HandleSwimming()
    {
        if (controller == null || !controller.enabled) return;
        velocity.y = 0;
        Transform camTransform = GetCameraTransform();
        if (camTransform == null) return;

        Vector3 forward = Vector3.ProjectOnPlane(
            camTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(
            camTransform.right, Vector3.up).normalized;

        Vector3 moveDir = (forward * inputPlayerHandler.inputmove.y) +(right * inputPlayerHandler.inputmove.x);

        float sSpeed = inputPlayerHandler.IsRun ? swimSpeed * 1.5f : swimSpeed;
        Vector3 motion = moveDir * sSpeed;

        float targetY = waterSurfaceY - 1.1f;
        motion.y = (targetY - transform.position.y) * 3f;

        controller.Move(motion * Time.deltaTime);
    }

    private void HandleBreathBar() => Hud?.ShowBreathBar();
    private void OnExitWater() => Hud?.HideBreathBar();

    private void UpdateAnimator()
    {
        if (animator == null) return;

        Vector2 input = inputPlayerHandler.inputmove;
        bool isMoving = input.magnitude > 0.1f;
        animator.SetBool("isMoving", isMoving);

        animator.SetBool("isSwim", currMovingStatus == movingStatus.Swimming);
        animator.SetBool("isClimb", currMovingStatus == movingStatus.Climbing);

        if (currMovingStatus == movingStatus.Walking)
        {
            bool isTrulyJumping = flagJump && !isNyentuhTanah && velocity.y > 0f;
            animator.SetBool("isJump", isTrulyJumping);

            bool isFalling = !isNyentuhTanah && !flagJump && velocity.y < -2f;
            animator.SetBool("isFall", isFalling);

            float dampTime = input.magnitude > 0.01f ? 0.05f : 0.02f;
            bool canRunAnim = CanRun();
            float runMult = (inputPlayerHandler.IsRun && canRunAnim) ? 1f : 0.5f;

            animator.SetFloat("SpeedX", input.x * runMult, dampTime, Time.deltaTime);
            animator.SetFloat("SpeedZ", input.y * runMult, dampTime, Time.deltaTime);
        }
        else
        {
            animator.SetBool("isJump", false);
            animator.SetBool("isFall", false);

            float dampTime = 0.05f;
            animator.SetFloat("SpeedX", input.x, dampTime, Time.deltaTime);
            animator.SetFloat("SpeedZ", input.y, dampTime, Time.deltaTime);

            if (currMovingStatus == movingStatus.Climbing)
                animator.SetFloat("SpeedY", input.y, dampTime, Time.deltaTime);
            else
                animator.SetFloat("SpeedY", 0f, dampTime, Time.deltaTime);
        }
        if (_animSync != null)
        {
            _animSync.SyncMovementParams(
                input.x, input.y,
                new Vector3(controller.velocity.x, 0, controller.velocity.z).magnitude,
                currMovingStatus == movingStatus.Swimming,
                currMovingStatus == movingStatus.Climbing);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        int layer = other.gameObject.layer;


        if (layer == LayerMask.NameToLayer("Laut"))
        {
            waterSurfaceY = other.bounds.max.y;
            Hud?.ShowBreathBar();
            Debug.Log("[Movement] Masuk air");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;


        if (other.gameObject.layer == LayerMask.NameToLayer("Laut"))
            OnExitWater();
    }

    private void SelesaiLadder()
    {
        isClimbing = false;
        animator?.SetBool("isClimb", false);
        Debug.Log("[Movement] SelesaiLadder");
    }
    
    }