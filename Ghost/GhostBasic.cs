using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkAnimator))]
public class GhostBasic : NetworkBehaviour
{
    protected GhostInputHandler ghostInputHandler;
    protected Animator animator;
    protected NetworkAnimator networkAnimator;
    protected CharacterController controller;
    protected CameraChanger cameraChanger;
    protected GhostHUD ghostHud;

    protected NetworkVariable<bool> netIsVisible = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    protected NetworkVariable<bool> netIsAttackMode = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    protected NetworkVariable<float> netAnimSpeed = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    protected NetworkVariable<bool> netAnimIsFly = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool isVisibility = false;
    public bool isAttackMode = false;
    public bool isFly = false;

    public float speedMove = 4f;
    public float speedRotate = 10f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    protected float _velocityY = 0f;

    protected int CurrentPhase => GamePhaseManager.Instance?.currentPhase.Value ?? 1;

    [Header("Skill Cooldowns")]
    public float appearCooldown = 20f;
    public float screamCooldown = 15f;
    public float teleportCooldown = 20f;
    public float specialSkillCooldown = 35f;
    public float attackCooldown = 30f;

    [Header("Appear Settings")]
    public float appearDuration = 1f;

    protected float _appearTimer;
    protected float _teleportTimer;
    protected float _specialTimer;
    protected float _screamTimer;
    protected float _attackTimer;

    [Header("Interact")]
    public float interactRadius = 2f;

    protected Plate nearbyPlate;
    protected PlayerMovement nearbyExorcist;
    protected Door nearbyDoor;
    protected NetworkLight nearbyLight;

    [Header("Attack")]
    public float attackRange = 2f;
    public int attackDamage = 1;
    public LayerMask exorcistLayer;

    [Header("Water Teleport")]
    public LayerMask waterLayer;
    public Transform waterFallbackPoint;

    [Header("Audio")]
    public AudioClip screamClip;
    public AudioSource screamAudioSource;

    [Header("SFX")]
    public AudioClip teleportSfx;
    public AudioClip attackSfx;
    public AudioSource sfxAudioSource;

    [Header("Ghost Type")]
    public GhostType ghostType;

    private float _appearVisibleTimer = 0f;
    private bool _tempVisible = false;
    private Transform _cachedGhostSpawn;

    [Header("Ground")]
    public LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    private bool _isGrounded;

    [HideInInspector] public float sensitivity = 30f;

    private GhostMoveState prevState;
    public enum GhostMoveState { Ground, Flying }
    public GhostMoveState currentState = GhostMoveState.Ground;

    protected static readonly int AnimSpeed = Animator.StringToHash("Speed");
    protected static readonly int AnimIsFly = Animator.StringToHash("isFly");
    protected static readonly int AnimAttack = Animator.StringToHash("Attack");
    protected static readonly int AnimIsFloat = Animator.StringToHash("isFLoat");

    protected virtual void Awake()
    {
        ghostInputHandler = GetComponent<GhostInputHandler>();
        animator = GetComponent<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
        controller = GetComponent<CharacterController>();
        cameraChanger = GetComponentInChildren<CameraChanger>(true);

        foreach (var cam in GetComponentsInChildren<Camera>(true)) cam.enabled = false;
        foreach (var al in GetComponentsInChildren<AudioListener>(true)) al.enabled = false;

        if (cameraChanger != null)
        {
            if (cameraChanger.ThirdPersonCamera != null) cameraChanger.ThirdPersonCamera.enabled = false;
            if (cameraChanger.FirstPersonCamera != null) cameraChanger.FirstPersonCamera.enabled = false;
        }

        if (sfxAudioSource == null) sfxAudioSource = screamAudioSource;

        var spawnGO = GameObject.FindWithTag("GhostSpawn");
        if (spawnGO != null) _cachedGhostSpawn = spawnGO.transform;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            foreach (var al in GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
            if (ghostInputHandler != null) ghostInputHandler.enabled = false;
            if (cameraChanger != null) cameraChanger.gameObject.SetActive(false);

            netAnimSpeed.OnValueChanged += OnNetAnimSpeedChanged;
            netAnimIsFly.OnValueChanged += OnNetAnimIsFlyChanged;
            netIsVisible.OnValueChanged += OnNetVisibilityChanged;
            netIsAttackMode.OnValueChanged += OnNetAttackModeChanged;
            return;
        }

        if (cameraChanger != null)
        {
            cameraChanger.gameObject.SetActive(true);
            foreach (var cam in cameraChanger.GetComponentsInChildren<Camera>(true))
            {
                cam.enabled = true;
                cam.tag = "MainCamera";
            }
            cameraChanger.InitCamera();
        }

        var myAudio = GetComponentInChildren<AudioListener>(true);
        if (myAudio != null) myAudio.enabled = true;

        ghostHud = FindFirstObjectByType<GhostHUD>();
        var exorcistHud = FindFirstObjectByType<HUD>();
        if (exorcistHud != null) exorcistHud.gameObject.SetActive(false);

        SetVisibilityLocal(false);

        netIsVisible.OnValueChanged += OnNetVisibilityChanged;
        netIsAttackMode.OnValueChanged += OnNetAttackModeChanged;

        StartCoroutine(InitHUDDelayed());
    }

    public override void OnNetworkDespawn()
    {
        netAnimSpeed.OnValueChanged -= OnNetAnimSpeedChanged;
        netAnimIsFly.OnValueChanged -= OnNetAnimIsFlyChanged;
        netIsVisible.OnValueChanged -= OnNetVisibilityChanged;
        netIsAttackMode.OnValueChanged -= OnNetAttackModeChanged;
    }

    protected virtual IEnumerator InitHUDDelayed()
    {
        int tries = 0;
        while (ghostHud == null && tries < 10)
        {
            ghostHud = FindFirstObjectByType<GhostHUD>();
            yield return new WaitForSeconds(0.2f);
            tries++;
        }

        if (ghostHud != null)
        {
            ghostHud.InitForGhost(ghostType,
                maxTeleport: teleportCooldown,
                maxHide: appearCooldown,
                maxScream: screamCooldown,
                maxSpecial: specialSkillCooldown,
                maxAttack: attackCooldown);
            ghostHud.SetRecipeDisplay(ghostType);
        }
        else Debug.LogWarning($"[GhostBasic] GhostHUD tidak ditemukan ({ghostType})");
    }

    protected virtual void Update()
    {
        if (!IsOwner) return;
        if (NotebookChatManager.IsTyping) return;

        TickCooldowns();
        DetectState();
        HandleStateExecution();

        if (ghostInputHandler != null)
        {
            HandleSkills();
            HandleInteractDetect();
            HandleInteractInput();
            HandleAppearTimer();
        }

        CheckWaterFall();
    }

    protected void DetectState()
    {
        prevState = currentState;
        currentState = isFly ? GhostMoveState.Flying : GhostMoveState.Ground;
        if (prevState != currentState) OnStateChanged();
    }

    protected void OnStateChanged()
    {
        if (currentState == GhostMoveState.Flying) _velocityY = 0f;
    }

    protected void HandleStateExecution()
    {
        switch (currentState)
        {
            case GhostMoveState.Ground: HandleGroundMovement(); break;
            case GhostMoveState.Flying: HandleFlyMovement(); break;
        }
    }

    private void TickCooldowns()
    {
        _appearTimer = Mathf.Max(0, _appearTimer - Time.deltaTime);
        _teleportTimer = Mathf.Max(0, _teleportTimer - Time.deltaTime);
        _specialTimer = Mathf.Max(0, _specialTimer - Time.deltaTime);
        _screamTimer = Mathf.Max(0, _screamTimer - Time.deltaTime);
        _attackTimer = Mathf.Max(0, _attackTimer - Time.deltaTime);
    }

    protected virtual void HandleGroundMovement()
    {
        if (ghostInputHandler == null) return;
        Transform camT = GetCameraTransform();
        if (camT == null) return;

        RotateToCamera(camT);
        Vector3 moveDir = GetCameraRelativeMove(camT);

        bool groundCheckValid = groundCheck != null && groundLayer != 0;
        _isGrounded = groundCheckValid
            ? Physics.CheckSphere(groundCheck.position, 0.3f, groundLayer)
            : controller.isGrounded;

        if (_isGrounded && _velocityY < 0) _velocityY = -2f;
        else _velocityY += gravity * Time.deltaTime;
        _velocityY = Mathf.Max(_velocityY, -20f);

        Vector3 motion = moveDir * speedMove;
        motion.y = _velocityY;
        controller.Move(motion * Time.deltaTime);

        SyncAnimSpeed(new Vector2(moveDir.x, moveDir.z).magnitude);
        SyncAnimIsFly(false);
    }

    protected virtual void HandleFlyMovement()
    {
        if (ghostInputHandler == null) return;
        _velocityY = 0f;
        Transform camT = GetCameraTransform();
        if (camT == null) return;

        RotateToCamera(camT);
        Vector3 moveDir = (camT.forward * ghostInputHandler.InputMove.y) +
                          (camT.right * ghostInputHandler.InputMove.x);
        controller.Move(moveDir * speedMove * Time.deltaTime);

        SyncAnimSpeed(moveDir.magnitude);
        SyncAnimIsFly(true);
    }

    protected void SyncAnimSpeed(float v)
    {
        if (animator != null) animator.SetFloat(AnimSpeed, v);
        if (!Mathf.Approximately(netAnimSpeed.Value, v)) netAnimSpeed.Value = v;
    }

    protected void SyncAnimIsFly(bool v)
    {
        if (animator != null) animator.SetBool(AnimIsFly, v);
        if (netAnimIsFly.Value != v) netAnimIsFly.Value = v;
    }

    private void OnNetAnimSpeedChanged(float _, float v)
    { if (!IsOwner && animator != null) animator.SetFloat(AnimSpeed, v); }

    private void OnNetAnimIsFlyChanged(bool _, bool v)
    { if (!IsOwner && animator != null) animator.SetBool(AnimIsFly, v); }

    protected void RotateToCamera(Transform camT)
    {
        Vector3 lookDir = camT.forward; lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            float rotateSpeed = sensitivity > 0 ? sensitivity : 20f;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);
        }
    }

    protected Vector3 GetCameraRelativeMove(Transform camT)
    {
        Vector3 forward = camT.forward; forward.y = 0; forward.Normalize();
        Vector3 right = camT.right; right.y = 0; right.Normalize();
        return (forward * ghostInputHandler.InputMove.y) + (right * ghostInputHandler.InputMove.x);
    }

    private void HandleSkills()
    {
        if (ghostInputHandler == null) return;
        int phase = CurrentPhase;

        if (ghostInputHandler.InvisibilityPressed)
        {
            ghostInputHandler.ResetInvisibility();
            if (_appearTimer <= 0f)
            {
                TriggerAppear();
                _appearTimer = appearCooldown;
                ghostHud?.StartCooldown(GhostHUD.SkillType.Hide);
            }
        }

        if (ghostInputHandler.TeleportPressed)
        {
            ghostInputHandler.ResetTeleport();
            if (_teleportTimer <= 0f)
            {
                TeleportToPlayerServerRpc();
                _teleportTimer = teleportCooldown;
                ghostHud?.StartCooldown(GhostHUD.SkillType.Teleport);
                PlaySfxServerRpc(SfxType.Teleport);
            }
        }

        if (ghostInputHandler.ScreamPressed)
        {
            ghostInputHandler.ResetScream();
            if (_screamTimer <= 0f)
            {
                _screamTimer = screamCooldown;
                ScreamServerRpc();
                ghostHud?.StartCooldown(GhostHUD.SkillType.Scream);
            }
        }

        if (ghostInputHandler.SpecialSkillPressed && phase >= 2)
        {
            ghostInputHandler.ResetSpecialSkill();
            if (_specialTimer <= 0f && TryUseSpecialSkillNearby())
            {
                _specialTimer = specialSkillCooldown;
                ghostHud?.StartCooldown(GhostHUD.SkillType.Special);
            }
        }

        if (ghostInputHandler.AttackModePressed && phase >= 3)
        {
            ghostInputHandler.ResetAttackMode();

            isAttackMode = !isAttackMode;
            SetAttackModeServerRpc(isAttackMode);
            ghostHud?.SetAttackMode(isAttackMode);

            if (isAttackMode)
                SetVisibilityLocal(true);
            else if (!_tempVisible)
                SetVisibilityServerRpc(false);
        }

        if (isAttackMode && ghostInputHandler.AttackClickedMouse && phase >= 3)
        {
            ghostInputHandler.ResetAttackClick();
            if (_attackTimer > 0f) return;

            HandleAttack();
            _attackTimer = attackCooldown;
            ghostHud?.StartCooldown(GhostHUD.SkillType.Attack);
            SetVisibilityLocal(true);
        }
    }

    private void TriggerAppear()
    {
        _tempVisible = true;
        _appearVisibleTimer = appearDuration;
        SetVisibilityServerRpc(true);
    }

    private void HandleAppearTimer()
    {
        if (!_tempVisible) return;
        _appearVisibleTimer -= Time.deltaTime;
        if (_appearVisibleTimer <= 0f)
        {
            _tempVisible = false;
            if (!isAttackMode) SetVisibilityServerRpc(false);
        }
    }

    protected virtual void HandleInteractDetect()
    {
        nearbyPlate = null; nearbyExorcist = null; nearbyDoor = null; nearbyLight = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRadius);
        foreach (var hit in hits)
        {
            if (nearbyPlate == null) nearbyPlate = hit.GetComponentInParent<Plate>();
            if (nearbyExorcist == null) nearbyExorcist = hit.GetComponentInParent<PlayerMovement>();
            if (nearbyDoor == null) nearbyDoor = hit.GetComponentInParent<Door>();
            if (nearbyLight == null) nearbyLight = hit.GetComponentInParent<NetworkLight>();
        }
        UpdateInteractHint();
    }

    private void UpdateInteractHint()
    {
        if (nearbyExorcist != null && CurrentPhase >= 2) { ghostHud?.OpenMessagePanel("[Q] Special Skill"); return; }
        if (nearbyPlate != null && !nearbyPlate.IsEmpty() && CurrentPhase >= 2) { ghostHud?.OpenMessagePanel("[F] Interact"); return; }
        if (nearbyDoor != null && CurrentPhase >= 2) { ghostHud?.OpenMessagePanel("[F] Interact"); return; }
        if (nearbyLight != null && CurrentPhase >= 2) { ghostHud?.OpenMessagePanel("[F] Interact"); return; }
        ghostHud?.CloseMessagePanel();
    }

    private void HandleInteractInput()
    {
        if (ghostInputHandler == null || !ghostInputHandler.InteractPressed) return;
        ghostInputHandler.ResetInteract();
        int phase = CurrentPhase;
        if (nearbyPlate != null && !nearbyPlate.IsEmpty() && phase >= 2)
        {
            var netObj = nearbyPlate.GetComponent<NetworkObject>();
            if (netObj != null) DisturbPlateServerRpc(netObj.NetworkObjectId);
            return;
        }
        if (phase >= 2 && nearbyDoor != null) { nearbyDoor.ToggleDoorServerRpc(); return; }
        if (phase >= 2 && nearbyLight != null) nearbyLight.ToggleLightServerRpc();
    }

    private void CheckWaterFall()
    {
        if (!Physics.CheckSphere(transform.position, 0.5f, waterLayer)) return;
        TeleportDirectServerRpc(GetWaterFallbackPosition());
    }

    private Vector3 GetWaterFallbackPosition()
    {
        if (waterFallbackPoint != null) return waterFallbackPoint.position;
        if (_cachedGhostSpawn == null)
        {
            var go = GameObject.FindWithTag("GhostSpawn");
            if (go != null) _cachedGhostSpawn = go.transform;
        }
        if (_cachedGhostSpawn != null) return _cachedGhostSpawn.position;
        var sp = SpawnManager.Instance?.GetSpawnPoint();
        return sp != null ? sp.position : transform.position + Vector3.up * 3f;
    }

    protected void SetVisibilityLocal(bool visible)
    {
        isVisibility = visible;
        GetComponent<GhostVisibilityManager>()?.SetVisible(visible);
    }

    private void OnNetVisibilityChanged(bool _, bool val)
    { if (!IsOwner) GetComponent<GhostVisibilityManager>()?.SetVisible(val); }

    private void OnNetAttackModeChanged(bool _, bool val)
    { if (!IsOwner && val) GetComponent<GhostVisibilityManager>()?.SetVisible(true); }

    [ServerRpc(RequireOwnership = false)]
    protected void SetVisibilityServerRpc(bool visible)
    {
        netIsVisible.Value = visible;
        SetVisibilityClientRpc(visible);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetAttackModeServerRpc(bool active)
    {
        netIsAttackMode.Value = active;
    }

    [ClientRpc]
    private void SetVisibilityClientRpc(bool visible)
    {
        isVisibility = visible;
        GetComponent<GhostVisibilityManager>()?.SetVisible(visible);
    }

    public enum SfxType { Scream, Teleport, Attack }

    [ServerRpc] protected void PlaySfxServerRpc(SfxType type) => PlaySfxClientRpc(type);

    [ClientRpc]
    private void PlaySfxClientRpc(SfxType type)
    {
        var source = sfxAudioSource != null ? sfxAudioSource : screamAudioSource;
        AudioClip clip = type switch
        {
            SfxType.Teleport => teleportSfx,
            SfxType.Attack => attackSfx,
            SfxType.Scream => screamClip,
            _ => null
        };
        if (source != null && clip != null) source.PlayOneShot(clip);
    }

    [ServerRpc] private void ScreamServerRpc() => ScreamClientRpc();

    [ClientRpc]
    private void ScreamClientRpc()
    {
        if (screamAudioSource != null && screamClip != null)
            screamAudioSource.PlayOneShot(screamClip);
    }

    [ServerRpc]
    protected virtual void TeleportToPlayerServerRpc()
    {
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Vector3 dest;
        if (players.Length > 0)
        {
            var t = players[Random.Range(0, players.Length)].transform;
            dest = t.position + t.forward * 2f;
            dest.y = transform.position.y;
        }
        else dest = GetWaterFallbackPosition();
        TeleportClientRpc(dest);
    }

    [ServerRpc] private void TeleportDirectServerRpc(Vector3 pos) => TeleportClientRpc(pos);

    [ClientRpc]
    protected void TeleportClientRpc(Vector3 pos)
    {
        _velocityY = 0f;
        if (controller != null)
        {
            controller.enabled = false;
            transform.position = pos;
            controller.enabled = true;
        }
        else transform.position = pos;
    }

    [ServerRpc]
    private void DisturbPlateServerRpc(ulong plateNetId)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(plateNetId, out var netObj)) return;
        var plate = netObj.GetComponent<Plate>(); if (plate == null) return;
        var item = plate.GetCurrentItem(); if (item == null) return;
        var itemNetObj = item.GetComponent<NetworkObject>(); if (itemNetObj == null) return;
        plate.RemoveItem();
        DropNearGhostClientRpc(itemNetObj.NetworkObjectId, transform.position);
    }

    [ClientRpc]
    private void DropNearGhostClientRpc(ulong itemNetId, Vector3 ghostPos)
    {
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetId, out var netObj)) return;
        var item = netObj.GetComponent<ItemPickUp>(); if (item == null) return;
        item.transform.position = ghostPos + Vector3.up * 0.5f;
        item.StopFollow();
        var rb = item.GetComponent<Rigidbody>();
        var col = item.GetComponent<Collider>();
        if (rb != null) { rb.isKinematic = false; rb.linearVelocity = Vector3.zero; }
        if (col != null) col.isTrigger = false;
    }

    protected virtual void HandleAttack()
    {
        if (animator != null) animator.SetTrigger(AnimAttack);
        TriggerAttackAnimServerRpc();
        PlaySfxServerRpc(SfxType.Attack);

        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, exorcistLayer);
        foreach (var hit in hits)
        {
            var health = hit.GetComponent<HealthSystem>();
            if (health != null && !health.IsDead()) health.TakeDamageServerRpc(attackDamage);
        }
    }

    [ServerRpc] private void TriggerAttackAnimServerRpc() => TriggerAttackAnimClientRpc();
    [ClientRpc]
    private void TriggerAttackAnimClientRpc()
    { if (!IsOwner && animator != null) animator.SetTrigger(AnimAttack); }

    protected Transform GetCameraTransform() =>
        cameraChanger != null ? cameraChanger.GetActiveCameraTransform() : null;

    protected void ShowPhaseLockedMessage(int required, string skill)
    {
        Debug.Log($"[Ghost] {skill} terkunci, phase sekarang: {CurrentPhase}");
    }

    protected virtual bool TryUseSpecialSkillNearby() { UseSpecialSkill(); return true; }
    protected virtual void UseSpecialSkill() { }
    public virtual void ApplyPassiveSkillToGhost(PlayerMovement target) { }
}