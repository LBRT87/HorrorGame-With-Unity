using Unity.Entities;

public enum ItemType
{
    Garam,Bamboo,Besi,Jarum,Garlic,Lidi,Gunting,Kelereng,Knife,Ikan,BananaLeaf,KelorLeaf,
    Medkit,
    Paper,
    Random,
    Banishment,
    Torch,
    Mirror,
    Paku,
    Spiritbox,
    OuijaBoard,
    Notebook,
}

public struct ItemComponent : IComponentData
{
    public ItemType Type;
    public int ItemID;
}

public struct PickupComponent : IComponentData
{
    public bool IsInteractable;
}
public struct GhostRecipeComponent
{
    public GhostType GhostType;
    public ItemType Item0;
    public ItemType Item1;
    public ItemType Item2;

    public ItemType[] ToArray() => new[] { Item0, Item1, Item2 };
}

public struct ActiveGhostsComponent
{
    public int Count;
    public GhostType Ghost0;
    public GhostType Ghost1;

    public GhostType[] ToArray()
    {
        if (Count == 2) return new[] { Ghost0, Ghost1 };
        return new[] { Ghost0 };
    }
}
