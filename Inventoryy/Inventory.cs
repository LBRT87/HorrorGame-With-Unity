using UnityEngine;

public class Inventory : MonoBehaviour
{
    public ItemPickUp[] slotInven = new ItemPickUp[2];
    private ItemPickUp _itemInHand = null;
    private ItemPickUp _followHand = null;

    public bool HasItemInHand() => _itemInHand != null;
    public ItemPickUp GetItemInHand() => _itemInHand;
    public bool IsSlotEmpty(int idx) =>
        idx >= 0 && idx < slotInven.Length && slotInven[idx] == null;

    private void LateUpdate()
    {
        if (_followHand == null) return;
        Transform hand = GetHandTransform();
        if (hand == null) return;
        _followHand.transform.position = hand.position;
        _followHand.transform.rotation = hand.rotation;
    }
    public bool TryPickUp(ItemPickUp item)
    {
        if (_itemInHand != null)
        {
            return false;
        }

        _itemInHand = item;
        AttachToHand(item);
        RefreshHandUI();
        return true;
    }
    public void TryUseMedkit()
    {
        ItemPickUp medkit = null;

        if (_itemInHand != null && _itemInHand.itemType == ItemType.Medkit)
        {
            medkit = _itemInHand;
            DetachFromHand(hide: false);
            _itemInHand = null;
            _followHand = null;
            RefreshHandUI();
        }
        else
        {
            for (int i = 0; i < slotInven.Length; i++)
            {
                if (slotInven[i] != null && slotInven[i].itemType == ItemType.Medkit)
                {
                    medkit = slotInven[i];
                    medkit.gameObject.SetActive(true);
                    slotInven[i] = null;
                    RefreshSlotUI(i);
                    break;
                }
            }
        }

        if (medkit == null)
        {
            Debug.Log("[Inventory] ga ada medkit");
            return;
        }

        var medkitItem = medkit.GetComponent<MedkitItem>();
        var health = GetComponent<HealthSystem>();

        if (medkitItem != null && health != null)
            medkitItem.UseOnTarget(health);
    }

    public bool TrySaveToSlot(int idx)
    {
        if (_itemInHand == null) return false;

        if (_itemInHand.itemType == ItemType.Torch)
        {
            Debug.Log("[Inventory] Torch gabisa simpn");
            return false;
        }

        if (slotInven[idx] != null)
        {
            return false;
        }

        slotInven[idx] = _itemInHand;

        DetachFromHand(hide: true);
        _itemInHand = null;

        RefreshSlotUI(idx);
        RefreshHandUI();
        return true;
    }

    public bool TryTakeFromSlot(int idx)
    {
        if (_itemInHand != null)
        {
            return false;
        }

        if (slotInven[idx] == null) return false;

        _itemInHand = slotInven[idx];
        slotInven[idx] = null;

        _itemInHand.gameObject.SetActive(true);
        AttachToHand(_itemInHand);

        RefreshSlotUI(idx);
        RefreshHandUI();
        return true;
    }

    public ItemPickUp TakeItemFromHand()
    {
        if (_itemInHand == null) return null;

        var item = _itemInHand;
        DetachFromHand(hide: false); 
        _itemInHand = null;
        RefreshHandUI();
        return item;
    }

    public ItemPickUp TakeItemFromSlot(int idx)
    {
        if (idx < 0 || idx >= slotInven.Length) 
            return null;
        if (slotInven[idx] == null) 
            return null;

        var item = slotInven[idx];
        slotInven[idx] = null;

        item.gameObject.SetActive(true);

        RefreshSlotUI(idx);
        return item;
    }

    public void DetachItemFromHand()
    {
        if (_itemInHand == null) return;

        Debug.Log($"[Inventory] DetachItemFromHand: {_itemInHand.name}");
        DetachFromHand(hide: false);

        _itemInHand = null;
        _followHand = null; 
        RefreshHandUI();
    }
    public ItemPickUp Getitem(int idx)
    {
        if (idx < 0 || idx >= slotInven.Length) return null;
        return slotInven[idx];
    }

    public bool HasItemType(ItemType type)
    {
        if (_itemInHand != null && _itemInHand.itemType == type) return true;
        foreach (var item in slotInven)
        {
            if (item != null && item.itemType == type)
            {
                return true;
            }
        }
        return false;
    }

    public ItemPickUp GetItemByType(ItemType type)
    {
        if (_itemInHand != null && _itemInHand.itemType == type) return _itemInHand;
        foreach (var item in slotInven)
        {
            if (item != null && item.itemType == type) 
                return item;

        }
        return null;
    }

    public int GetSlotIndex(ItemPickUp item)
    {
        for (int i = 0; i < slotInven.Length; i++)
            if (slotInven[i] == item) return i;
        return -1;
    }

    public void RefreshAllUI()
    {
        for (int i = 0; i < slotInven.Length; i++)
            RefreshSlotUI(i);
    }

    private void AttachToHand(ItemPickUp item)
    {
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (item.TryGetComponent<Collider>(out var col))
            col.enabled = false;

        Transform hand = GetHandTransform();
        if (hand != null)
        {
            item.transform.position = hand.position;
            item.transform.rotation = hand.rotation;
        }

        _followHand = item;
    }

    private void DetachFromHand(bool hide)
    {
        if (_itemInHand == null) return;

        _followHand = null;

        if (hide)
        {
            _itemInHand.gameObject.SetActive(false);
        }
        else
        {
            if (_itemInHand.TryGetComponent<Collider>(out var col))
                col.enabled = true;
        }
    }

    private Transform GetHandTransform()
    {
        var pm = GetComponent<PlayerMovement>();
        return pm?.Hand ?? transform;
    }

    private void RefreshSlotUI(int idx)
    {
        bool hasItem = slotInven[idx] != null;
        HUDManager.Instance?.GetExorcistHUD()?.UpdateSlot(idx, hasItem);
    }

    private void RefreshHandUI() { }
}