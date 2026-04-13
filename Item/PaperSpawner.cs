using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PaperSpawner : NetworkBehaviour
{
    public static PaperSpawner Instance;

    public GameObject paperPrefab;
    public Transform[] paperSpawnPoints;


    private static readonly Dictionary<GhostType, string> GhostHints = new()
    {
        { GhostType.Kuntilanak,  "The Wailing Woman"     },
        { GhostType.Tuyul,       "The Scheming Child"    },
        { GhostType.WeweGombel,  "The Unfigure Giant"    },
    };

    private static readonly Dictionary<ItemType, string> ItemHints = new()
    {
        { ItemType.Garam,       "White grains from the sea it burns."         },
        { ItemType.Bamboo,      "The hollow green shoot traps the spirit."    },
        { ItemType.Besi,        "Cold iron chains bears its rage."            },
        { ItemType.Jarum,       "It's scared of pointy items."                },
        { ItemType.Garlic,      "The pungent smell drives it away."           },
        { ItemType.Lidi,        "A broom of sticks sweeps the spirit away."  },
        { ItemType.Gunting,     "Their rags it cuts."                         },
        { ItemType.Kelereng,    "Shiny orbs that distract the spirit."        },
        { ItemType.Knife,       "A sharp blade to fend it off."               },
        { ItemType.Ikan,        "Something that reminds it of the sea."       },
        { ItemType.BananaLeaf,  "A large leaf that can cover it."             },
        { ItemType.KelorLeaf,   "A bitter leaf that repels it."               },
    };

    private static readonly GhostType[] AllGhosts = new[]
    {
        GhostType.Kuntilanak, GhostType.Tuyul, GhostType.WeweGombel
    };

    private void Awake() => Instance = this;

    public void SpawnPapers()
    {
        if (!IsServer) return;

        var registry = GameRecipeRegistry.Instance;
        if (registry == null) { Debug.LogError("[PaperSpawner] Registry null!"); return; }

        var activeGhosts = new HashSet<GhostType>(registry.GetActiveGhosts());

        var paperContents = new List<(string ghostHint, string itemHint)>();

        foreach (var ghost in registry.GetActiveGhosts())
        {
            string ghostHint = GhostHints.TryGetValue(ghost, out var gh) ? gh : ghost.ToString();
            foreach (var item in registry.GetRecipeFor(ghost))
            {
                string itemHint = ItemHints.TryGetValue(item, out var ih) ? ih : item.ToString();
                paperContents.Add((ghostHint, itemHint));
            }
        }

        var inactiveGhosts = new List<GhostType>();
        foreach (var g in AllGhosts)
            if (!activeGhosts.Contains(g)) inactiveGhosts.Add(g);

        var usedItems = new HashSet<ItemType>(registry.GetAllRequiredItems());
        var unusedItems = new List<ItemType>();
        foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            if (ItemHints.ContainsKey(t) && !usedItems.Contains(t))
                unusedItems.Add(t);
        Shuffle(unusedItems);

        int redHerringIdx = 0;
        while (paperContents.Count < 9 && inactiveGhosts.Count > 0)
        {
            var ghost = inactiveGhosts[redHerringIdx % inactiveGhosts.Count];
            string ghostHint = GhostHints.TryGetValue(ghost, out var gh) ? gh : ghost.ToString();
            string itemHint = unusedItems.Count > 0
                ? ItemHints[unusedItems[redHerringIdx % unusedItems.Count]]
                : "The spirit remains elusive.";
            paperContents.Add((ghostHint, itemHint));
            redHerringIdx++;
        }

        Shuffle(paperContents);

        var pts = new List<Transform>(paperSpawnPoints);
        Shuffle(pts);

        int spawnCount = Mathf.Min(9, paperContents.Count, pts.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnOnePaperClientRpc(pts[i].position, pts[i].rotation,
                paperContents[i].ghostHint, paperContents[i].itemHint);
        }

        Debug.Log($"[PaperSpawner] Total paper : {spawnCount}"); 
    }

    [ClientRpc]
    private void SpawnOnePaperClientRpc(Vector3 pos, Quaternion rot, string ghostHint, string itemHint)
    {
        var obj = Instantiate(paperPrefab, pos, rot);
        var paper = obj.GetComponent<PaperItem>();
        if (paper != null)
        {
            paper.SetContent(ghostHint, itemHint);
            Debug.Log($"[PaperSpawner][Client] Paper spawn di {pos} → {ghostHint} | {itemHint}");
        }
        else
        {
            Debug.LogWarning($"[PaperSpawner][Client] PaperItem gada prefab"); 
        }
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