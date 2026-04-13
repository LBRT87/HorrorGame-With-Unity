using Unity.Netcode;
using UnityEngine;

public class InteractionController : NetworkBehaviour
{
    private HUD _hud;
    private PlayerMovement _movement;
    private Inventory _inventory;
    private HealthSystem _health;
    private PlayerInputHandler _input;
    private ExorcistSoundEffects _soundFX;

    private ItemPickUp _nearItem;
    private Plate _nearPlate;
    private InteractionController _nearExorcist;
    private Door _nearDoor;

    [Header("Throw")]
    [SerializeField] private float throwForce = 10f;

    [Header("Torch")]
    [SerializeField] private GameObject torchPrefab;
    private GameObject _torchInstance;
    private bool _torchOn = false;

    [Header("Heal")]
    [SerializeField] private float healDuration = 4f;
    private bool _isHealingSelf = false;
    private float _healSelfTimer = 0f;
    private bool _isHealingOther = false;
    private float _healOtherTimer = 0f;

    private PaperItem _nearPaper;
    private bool _paperPanelOpen = false;
    private bool _torchOnRemote = false;
    void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _inventory = GetComponent<Inventory>();
        _input = GetComponent<PlayerInputHandler>();
        _health = GetComponent<HealthSystem>();
        _soundFX = GetComponent<ExorcistSoundEffects>(); 
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;
        _hud = FindFirstObjectByType<HUD>();
    }
    public override void OnNetworkDespawn()
    {
        if (_torchInstance != null)
        {
            Destroy(_torchInstance);
            _torchInstance = null;
        }
        base.OnNetworkDespawn();
    }

    void Update()
    {
        HandleTorchFollow();
        if (!IsOwner) return;
        if (_hud == null) _hud = FindFirstObjectByType<HUD>();

        HandleMessagePanel();
        HandlePickUp();
        HandleSlotInput();
        HandleThrow();
        HandleInteract();
        HandleTorch();
        HandleHeal();
    }

    private void OnDestroy()
    {
        if (_torchInstance != null) Destroy(_torchInstance);
    }

    private void HandleTorchFollow()
    {
        bool shouldFollow = IsOwner ? _torchOn : _torchOnRemote;
        if (!shouldFollow || _torchInstance == null) return;

        Transform hand = _movement?.Hand ?? transform;
        if (hand == null) return;
        _torchInstance.transform.position = hand.position;
        _torchInstance.transform.rotation = hand.rotation;
    }

    private void HandlePickUp()
    {
        if (!_input.PickPressed) return;
        _input.ResetPick();
        if (_nearItem == null || _nearItem.isPicked) return;

        if (_inventory.HasItemInHand() || _torchOn)
        {
            return;
        }
        if (_health != null && _health.currentHealth.Value <= 1)
        {
            return;
        }
        bool added = _inventory.TryPickUp(_nearItem);
        if (added) _nearItem.RequestPickUp(OwnerClientId);
    }

    private void HandleSlotInput()
    {
        if (_input.Slot1Pressed) { _input.ResetSlot1(); HandleSlot(0); }
        if (_input.Slot2Pressed) { _input.ResetSlot2(); HandleSlot(1); }
    }

    private void HandleSlot(int idx)
    {
        if (_inventory.HasItemInHand())
        {
            var special = GetComponent<StarterItemSystem>();

            bool saved = _inventory.TrySaveToSlot(idx);
            if (!saved)
            {
                if (_inventory.GetItemInHand()?.itemType == ItemType.Torch)
                    _hud?.OpenMessagePanel("");
                else
                    _hud?.OpenMessagePanel("");
            }
        }
        else
        {
            bool taken = _inventory.TryTakeFromSlot(idx);
            if (taken)
            {
                var item = _inventory.GetItemInHand();
                if (item != null) item.RequestPickUp(OwnerClientId);
            }
            else
            {
                _hud?.OpenMessagePanel("");
            }
        }
    }

    private void HandleThrow()
    {
        if (!_input.ThrowPressed) return;
        _input.ResetThrow();
        if (!_inventory.HasItemInHand())
        {
            _hud?.OpenMessagePanel("");
            return;
        }
        if (_movement?.currMovingStatus == PlayerMovement.movingStatus.Swimming)
        {
            _hud?.OpenMessagePanel("");
            return;
        }
        var item = _inventory.TakeItemFromHand();
        if (item != null) item.RequestThrow(GetThrowDirection(), throwForce);
    }

    private Vector3 GetThrowDirection()
    {
        Transform cam = Camera.main?.transform;
        if (cam != null)
        {
            Vector3 d = cam.forward;
            d.y += 0.2f;
            return d.normalized;
        }
        return transform.forward + Vector3.up * 0.2f;
    }

    private void HandleInteract()
    {
        if (!_input.InteractPressed) return;
        _input.ResetInteract();

        if (_nearDoor != null) { _nearDoor.ToggleDoorServerRpc(); return; }
        if (_nearPlate != null) { HandlePlateInteract(); return; }

        if (_nearPaper != null)
        {
            _paperPanelOpen = !_paperPanelOpen;
            if (_paperPanelOpen)
                _hud?.ShowPaperPanel(_nearPaper._ghostHint, _nearPaper._itemHint);
            else
                _hud?.HidePaperPanel();
            return;
        }
    }

    private void HandlePlateInteract()
    {
        if (_nearPlate == null) return;

        if (_nearPlate.HasItem())
        {
            if (_inventory.HasItemInHand())
            {
                return;
            }
            var item = _nearPlate.GetCurrentItem();
            if (item == null) return;
            bool picked = _inventory.TryPickUp(item);
            if (picked) _nearPlate.RequestTakeItem(OwnerClientId);
        }
        else if (_inventory.HasItemInHand())
        {
            var heldItem = _inventory.GetItemInHand();
            if (heldItem == null) return;

            if (heldItem.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            _inventory.DetachItemFromHand();
            heldItem.RequestDrop();
            _nearPlate.RequestPlaceItem(heldItem);
        }
    }

    private void HandleTorch()
    {
        if (!_input.TorchPressed) return;
        _input.ResetTorch();

        if (_inventory.HasItemInHand()) return;

        _torchOn = !_torchOn;
        ToggleTorchLocal(_torchOn);
        SetTorchServerRpc(_torchOn);
    }

    private void ToggleTorchLocal(bool active)
    {
        if (active)
        {
            if (_torchInstance == null)
            {
                if (torchPrefab == null) return;

                Transform spawnParent = IsOwner ? (_movement?.Hand ?? transform) : null;
                Vector3 spawnPos = _movement?.Hand?.position ?? transform.position;
                Quaternion spawnRot = _movement?.Hand?.rotation ?? transform.rotation;

                _torchInstance = spawnParent != null ? Instantiate(torchPrefab, spawnParent)
                    : Instantiate(torchPrefab, spawnPos, spawnRot);
            }
            _torchInstance.SetActive(true);
        }
        else
        {
            if (_torchInstance != null)
                _torchInstance.SetActive(false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetTorchServerRpc(bool active) => SetTorchClientRpc(active);

    [ClientRpc]
    private void SetTorchClientRpc(bool active)
    {
        if (IsOwner) return;
        _torchOnRemote = active;
        ToggleTorchLocal(active);
    }
    private void LateUpdate() 
    {
        HandleTorchFollow();
    }

    private void HandleHeal()
    {
        if (_health == null || _input == null) return;

        bool holdingMedkit = IsHoldingMedkit();
        bool holdingH = _input.IsHealHeld;

        if (holdingMedkit && _health.currentHealth.Value < HealthSystem.MAX_HEALTH)
            _hud?.ShowHealPanel();
        else
            _hud?.HideHealPanel();

        if (!holdingMedkit)
        {
            ResetHealSelf();
            ResetHealOther();
            return;
        }

        bool canHealOther = _nearExorcist != null &&
                            _nearExorcist._health != null &&
                            !_nearExorcist._health.IsDead() &&
                            _nearExorcist._health.currentHealth.Value < HealthSystem.MAX_HEALTH;

        if (canHealOther && holdingH)
        {
            _isHealingOther = true;
            _healOtherTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_healOtherTimer / healDuration);
            _hud?.ShowHealProgress(progress);

            if (_healOtherTimer >= healDuration)
            {
                _healOtherTimer = 0f;
                _isHealingOther = false;
                _hud?.HideHealProgress();
                ConsumeMedkit();
                _nearExorcist.ReceiveHealServerRpc(1);
            }
            return;
        }
        else if (!holdingH) ResetHealOther();

        bool canHealSelf = _health.currentHealth.Value < HealthSystem.MAX_HEALTH;

        if (canHealSelf && holdingH)
        {
            _isHealingSelf = true;
            _healSelfTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_healSelfTimer / healDuration);
            _hud?.ShowHealProgress(progress);

            if (_healSelfTimer >= healDuration)
            {
                _healSelfTimer = 0f;
                _isHealingSelf = false;
                _hud?.HideHealProgress();
                ConsumeMedkit();
                _health.HealServerRpc(1);
            }
            return;
        }

        if (_health.currentHealth.Value >= HealthSystem.MAX_HEALTH)
        {
            ResetHealSelf();
            ResetHealOther();
        }
    }

    private void ResetHealSelf()
    {
        if (!_isHealingSelf) return;
        _isHealingSelf = false;
        _healSelfTimer = 0f;
        _hud?.HideHealProgress();
    }

    private void ResetHealOther()
    {
        if (!_isHealingOther) return;
        _isHealingOther = false;
        _healOtherTimer = 0f;
        _hud?.HideHealProgress();
    }

    private bool IsHoldingMedkit() => _inventory.HasItemType(ItemType.Medkit);

    private void ConsumeMedkit()
    {
        var medkit = _inventory.GetItemByType(ItemType.Medkit);
        if (medkit == null) return;

        if (_inventory.GetItemInHand() == medkit)
            _inventory.TakeItemFromHand();
        else
        {
            int slot = _inventory.GetSlotIndex(medkit);
            if (slot != -1) _inventory.TakeItemFromSlot(slot);
        }

        var netObj = medkit.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            _health.DespawnMedkitServerRpc(netObj.NetworkObjectId);
        }
        else
        {
            Destroy(medkit.gameObject);
        }

        Debug.Log("[Interaction] Medkit dah dipke");
    }
    private void HandleMessagePanel()
    {
        if (_nearPaper != null)
        {
            _hud?.OpenMessagePanel("[F] Interact");
            return;
        }
        if (_nearDoor != null)
        {
            _hud?.OpenMessagePanel("[F] Interact");
            return;
        }
        if (_nearPlate != null)
        {
            if (_nearPlate.HasItem()) _hud?.OpenMessagePanel("[F] Interact");
            else if (_inventory.HasItemInHand()) _hud?.OpenMessagePanel("[F] Interact");
            else _hud?.CloseMessagePanel();
            return;
        }
        if (_nearExorcist != null && IsHoldingMedkit())
        {
            _hud?.OpenMessagePanel("[H] Hold to Heal Friend");
            return;
        }
        if (_nearItem != null && !_nearItem.isPicked)
        {
            if (_inventory.HasItemInHand()) _hud?.OpenMessagePanel("");
            else _hud?.OpenMessagePanel("[R] Pickup Item");
            return;
        }
        if (IsHoldingMedkit() && _health != null &&
            _health.currentHealth.Value < HealthSystem.MAX_HEALTH)
        {
            _hud?.OpenMessagePanel("[H] Hold to Heal");
            return;
        }
        if (!_inventory.HasItemInHand())
        {
            return;
        }
        _hud?.CloseMessagePanel();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReceiveHealServerRpc(int amount) => _health?.HealServerRpc(amount);

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;

        var item = other.GetComponentInParent<ItemPickUp>();
        if (item != null && !item.isPicked) { _nearItem = item; return; }

        var plate = other.GetComponentInParent<Plate>();
        if (plate != null) { _nearPlate = plate; return; }

        var door = other.GetComponentInParent<Door>();
        if (door != null) { _nearDoor = door; return; }

        var otherIC = other.GetComponentInParent<InteractionController>();
        if (otherIC != null && otherIC != this) _nearExorcist = otherIC;

        var paper = other.GetComponentInParent<PaperItem>();
        if (paper != null) { _nearPaper = paper; return; }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;

        var item = other.GetComponentInParent<ItemPickUp>();
        if (item != null && item == _nearItem) { _nearItem = null; _hud?.CloseMessagePanel(); return; }

        var plate = other.GetComponentInParent<Plate>();
        if (plate != null && plate == _nearPlate) { _nearPlate = null; _hud?.CloseMessagePanel(); return; }

        var door = other.GetComponentInParent<Door>();
        if (door != null && door == _nearDoor) { _nearDoor = null; _hud?.CloseMessagePanel(); return; }

        var otherIC = other.GetComponentInParent<InteractionController>();
        if (otherIC != null && otherIC == _nearExorcist)
        {
            _nearExorcist = null;
            ResetHealOther();
        }

        var paper = other.GetComponentInParent<PaperItem>();
        if (paper != null && paper == _nearPaper)
        {
            _nearPaper = null;
            _hud?.HidePaperPanel();
            _paperPanelOpen = false;
            return;
        }
    }
}