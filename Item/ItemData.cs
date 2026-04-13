using UnityEngine;

[CreateAssetMenu(menuName = "Item/Data")]
public class ItemData : ScriptableObject
{
    public ItemType itemType;  
    public GameObject prefab;   

}