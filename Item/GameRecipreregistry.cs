using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public class GameRecipeRegistry : NetworkBehaviour
{
    public static GameRecipeRegistry Instance;
    private ActiveGhostsComponent _activeGhosts;
    private readonly Dictionary<GhostType, GhostRecipeComponent> _recipes = new Dictionary<GhostType, GhostRecipeComponent>();

    private static readonly ItemType[] BanishmentPool = new[]
    {
        ItemType.Garam,      ItemType.Bamboo,   ItemType.Besi,
        ItemType.Jarum,      ItemType.Garlic,   ItemType.Lidi,
        ItemType.Gunting,    ItemType.Kelereng, ItemType.Knife,
        ItemType.Ikan,       ItemType.BananaLeaf, ItemType.KelorLeaf,
    };

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    public ItemType[] GetFullBanishmentPool() => BanishmentPool;

    public void GenerateRecipes(GhostType[] activeGhosts)
    {
        if (!IsServer) return;

        _activeGhosts = new ActiveGhostsComponent
        {
            Count = activeGhosts.Length,
            Ghost0 = activeGhosts[0],
            Ghost1 = activeGhosts.Length > 1 ? activeGhosts[1] : GhostType.Kuntilanak
        };

        _recipes.Clear();

        var pool = new List<ItemType>(BanishmentPool);
        Shuffle(pool);

        int idx = 0;
        foreach (var ghost in activeGhosts)
        {
            var recipe = new GhostRecipeComponent
            {
                GhostType = ghost,
                Item0 = pool[idx++],
                Item1 = pool[idx++],
                Item2 = pool[idx++],
            };
            _recipes[ghost] = recipe;
        }
        Debug.Log($"[Registry] Total ghost aktif: {activeGhosts.Length}");

        foreach (var ghost in activeGhosts)
        {
            if (_recipes.TryGetValue(ghost, out var r))
            {
                Debug.Log($"[Registry] Ghost: {ghost}");
                Debug.Log($"[Registry] Recipe: {r.Item0}, {r.Item1}, {r.Item2}");
            }
        }

        Debug.Log($"[Registry] Total item utk ritual: {GetAllRequiredItems().Count}");

        SyncRecipesToClients();
    }
    public GhostType[] GetActiveGhosts() => _activeGhosts.ToArray();

    public ItemType[] GetRecipeFor(GhostType ghost)
    {
        if (_recipes.TryGetValue(ghost, out var r)) return r.ToArray();
        return new ItemType[0];
    }
    public List<ItemType> GetAllRequiredItems()
    {
        var list = new List<ItemType>();
        foreach (var ghost in _activeGhosts.ToArray())
        {
            if (_recipes.TryGetValue(ghost, out var r))
            {
                list.Add(r.Item0);
                list.Add(r.Item1);
                list.Add(r.Item2);
            }
        }
        return list;
    }

    public bool IsRequiredItem(ItemType type) => GetAllRequiredItems().Contains(type);

    private void SyncRecipesToClients()
    {
        var ghosts = _activeGhosts.ToArray();

        if (ghosts.Length == 1)
        {
            var r0 = _recipes[ghosts[0]];
            SyncRecipeOneGhostClientRpc(
                ghosts[0],
                r0.Item0, r0.Item1, r0.Item2
            );
        }
        else
        {
            var r0 = _recipes[ghosts[0]];
            var r1 = _recipes[ghosts[1]];
            SyncRecipeTwoGhostsClientRpc(
                ghosts[0], r0.Item0, r0.Item1, r0.Item2,
                ghosts[1], r1.Item0, r1.Item1, r1.Item2
            );
        }
    }

    [ClientRpc]
    private void SyncRecipeOneGhostClientRpc(
        GhostType g0, ItemType i0, ItemType i1, ItemType i2)
    {
        if (IsServer) return;
        _activeGhosts = new ActiveGhostsComponent { Count = 1, Ghost0 = g0 };
        _recipes[g0] = new GhostRecipeComponent { GhostType = g0, Item0 = i0, Item1 = i1, Item2 = i2 };

        Debug.Log($"[Registry][Client] dpt recipe:");
        Debug.Log($"[Registry][Client] Ghost: {g0} → {i0}, {i1}, {i2}");
    }

    [ClientRpc]
    private void SyncRecipeTwoGhostsClientRpc(
        GhostType g0, ItemType i0a, ItemType i0b, ItemType i0c,
        GhostType g1, ItemType i1a, ItemType i1b, ItemType i1c)
    {
        if (IsServer) return;
        _activeGhosts = new ActiveGhostsComponent { Count = 2, Ghost0 = g0, Ghost1 = g1 };
        _recipes[g0] = new GhostRecipeComponent { GhostType = g0, Item0 = i0a, Item1 = i0b, Item2 = i0c };
        _recipes[g1] = new GhostRecipeComponent { GhostType = g1, Item0 = i1a, Item1 = i1b, Item2 = i1c };

        Debug.Log($"[Registry][Client] dpt recipe:");
        Debug.Log($"[Registry][Client] Ghost: {g0} → {i0a}, {i0b}, {i0c}");
        Debug.Log($"[Registry][Client] Ghost: {g1} → {i1a}, {i1b}, {i1c}");
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}