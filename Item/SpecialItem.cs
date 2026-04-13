using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class StarterItemSystem : NetworkBehaviour
{
    [SerializeField] private Transform itemHoldPoint;
    [SerializeField] private GameObject spiritBoxHandPrefab;
    [SerializeField] private GameObject notebookHandPrefab;

    public NetworkVariable<StarterItemType> AssignedItem = new NetworkVariable<StarterItemType>(
        StarterItemType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<StarterItemType> HeldItem = new NetworkVariable<StarterItemType>(
        StarterItemType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject _spawnedItemObj;
    private bool _isHolding = false;

    private PlayerVoiceAdapter _voiceAdapter;
    private NotebookChatManager _notebookChat;
    private PlayerInputHandler _inputHandler;

    public override void OnNetworkSpawn()
    {
        HeldItem.OnValueChanged += OnHeldItemChanged;

        if (!IsOwner) return;

        _voiceAdapter = GetComponent<PlayerVoiceAdapter>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _notebookChat = FindFirstObjectByType<NotebookChatManager>();
    }

    public override void OnNetworkDespawn()
    {
        HeldItem.OnValueChanged -= OnHeldItemChanged;
    }

    private void Update()
    {
        if (!IsOwner)
            return;
        if (AssignedItem.Value == StarterItemType.None)
            return;
        if (_inputHandler == null) 
            return;
        if (_inputHandler.SpecialItemPressed)
        {
            _inputHandler.ResetSpecialItem(); 

            ToggleHoldItem();
        }
    }

    private void ToggleHoldItem()
    {
        if (_isHolding)
            DropItem();
        else
            HoldItem();
    }


    private void HoldItem()
    {
        if (AssignedItem.Value == StarterItemType.None) return;

        if (HeldItem.Value != StarterItemType.None)
        {
            return;
        }

        _isHolding = true;
        SetHeldItemServerRpc(AssignedItem.Value);
        SpawnItemVisualLocal(AssignedItem.Value);
        ActivateItemSystem(AssignedItem.Value, true);
        Debug.Log($"[StarterItemSystem] Hold: {AssignedItem.Value}");
    }

    private void DropItem()
    {
        if (!_isHolding) return;

        ActivateItemSystem(HeldItem.Value, false);
        _isHolding = false;
        SetHeldItemServerRpc(StarterItemType.None);
        DestroyItemVisualLocal();
        Debug.Log("[StarterItemSystem] Drop item");
    }


    [ServerRpc(RequireOwnership = false)]
    private void SetHeldItemServerRpc(StarterItemType itemType)
    {
        HeldItem.Value = itemType;
    }

    [ServerRpc(RequireOwnership = false)]
    public void AssignStarterItemServerRpc(StarterItemType itemType)
    {
        AssignedItem.Value = itemType;
        Debug.Log($"[StarterItemSystem] Assigned {itemType} → client {OwnerClientId}");
    }


    private void SpawnItemVisualLocal(StarterItemType itemType)
    {
        StartCoroutine(SpawnVisualDelayed(itemType));
    }

    private IEnumerator SpawnVisualDelayed(StarterItemType itemType)
    {
        float timeout = 3f;
        while (itemHoldPoint == null && timeout > 0)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
            itemHoldPoint = transform.Find("ItemHoldPoint");
        }

        if (itemHoldPoint == null)
        {
            Debug.LogError("[StarterItemSystem] itemHoldPoint null");
            yield break;
        }

        DestroyItemVisualLocal();

        GameObject prefab = itemType switch
        {
            StarterItemType.SpiritBox => spiritBoxHandPrefab,
            StarterItemType.Notebook => notebookHandPrefab,
            _ => null
        };

        if (prefab == null)
        {
            Debug.Log($"[StarterItemSystem] g ada prefab untuk {itemType}");
            yield break;
        }

        _spawnedItemObj = Instantiate(prefab, itemHoldPoint);
        _spawnedItemObj.transform.localPosition = Vector3.zero;
        _spawnedItemObj.transform.localRotation = Quaternion.identity;
        Debug.Log($"[StarterItemSystem] Visual spawned: {itemType}");
    }

    private void DestroyItemVisualLocal()
    {
        if (_spawnedItemObj != null)
        {
            Destroy(_spawnedItemObj);
            _spawnedItemObj = null;
        }
    }


    private void ActivateItemSystem(StarterItemType itemType, bool active)
    {
        switch (itemType)
        {
            case StarterItemType.SpiritBox:
                ActivateSpiritBox(active);
                break;
            case StarterItemType.Notebook:
                ActivateNotebook(active);
                break;
        }
    }

    private void ActivateSpiritBox(bool active)
    {
        if (_voiceAdapter == null)
            _voiceAdapter = GetComponent<PlayerVoiceAdapter>();

        if (_voiceAdapter != null)
            _voiceAdapter.SetVoiceActive(active);
        else
            Debug.LogWarning("[StarterItemSystem] PlayerVoiceAdapter gad ");
    }

    private void ActivateNotebook(bool active)
    {
        if (_notebookChat == null)
            _notebookChat = FindFirstObjectByType<NotebookChatManager>();

        if (_notebookChat != null)
        {
            _notebookChat.SetExorcistHolding(active);
        }
    }


    private void OnHeldItemChanged(StarterItemType prev, StarterItemType next)
    {
        Debug.Log($"[StarterItemSystem] HeldItem: {prev} = {next}");
        OnItemChanged?.Invoke(next);
    }


    public bool HasItem(StarterItemType itemType) => AssignedItem.Value == itemType;
    public bool HasAnyItem() => AssignedItem.Value != StarterItemType.None;
    public bool IsHolding(StarterItemType itemType) => HeldItem.Value == itemType;
    public bool IsHoldingAny() => HeldItem.Value != StarterItemType.None;

    public System.Action<StarterItemType> OnItemChanged;
}