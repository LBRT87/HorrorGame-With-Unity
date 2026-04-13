using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemSpawner : NetworkBehaviour
{
    public static ItemSpawner Instance;

    public GameObject[] allItemPrefabs;   
    public GameObject[] randomItemPrefabs; 
    public GameObject medkitPrefab;
    public Transform[] itemSpawnPoints;  

    public Transform[] cheatSpawnPoints;
    private void Awake() => Instance = this;
    public void SpawnAllItems()
    {
        if (!IsServer) return;

        var registry = GameRecipeRegistry.Instance;
        if (registry == null) {
            Debug.LogError("[ItemSpawner] Registry null"); 
            return; 
        }

        var pts = new List<Transform>(itemSpawnPoints);
        Shuffle(pts);
        int ptIdx = 0;

        Debug.Log($"[ItemSpawner] spawn banihsment item");
        int cont = 0;
        foreach (var item in registry.GetFullBanishmentPool())
        {
            var prefab = GetPrefabByType(item);
            if (prefab == null) { Debug.LogWarning($"[ItemSpawner] Prefab {item} tidak ada!"); continue; }
            SpawnItem(prefab, pts[ptIdx++ % pts.Count].position, "Banishment");
            Debug.Log("spwn banishment: " + item + "ke - "+ cont++) ;
        }

        Debug.Log($"[ItemSpawner] spanwmedkit");
        for (int i = 0; i < 4; i++)
        {
            SpawnItem(medkitPrefab, pts[ptIdx++ % pts.Count].position, "Medkit");
            Debug.Log("Spawn medkit ke -"+i);

        }

        Debug.Log($"[ItemSpawner] === Spawn Random Items ===");
        var shuffledRandom = new List<GameObject>(randomItemPrefabs);
        Shuffle(shuffledRandom);
        for (int i = 0; i < Mathf.Min(6, shuffledRandom.Count); i++)
        {
            SpawnItem(shuffledRandom[i % shuffledRandom.Count], pts[ptIdx++ % pts.Count].position, "Random");
            Debug.Log("Spawn random item ke - " + i); 

        }

        Debug.Log($"[ItemSpawner] total item spawn: {ptIdx} ");
    }

    public void CheatSpawnBanishment()
    {
        if (!IsServer) return;

        var registry = GameRecipeRegistry.Instance;
        if (registry == null) return;

        var spawnPts = (cheatSpawnPoints != null && cheatSpawnPoints.Length > 0)? cheatSpawnPoints: itemSpawnPoints;

        int idx = 0;
        foreach (var item in registry.GetAllRequiredItems())
        {
            var prefab = GetPrefabByType(item);
            if (prefab == null) continue;
            Vector3 pos = spawnPts[idx % spawnPts.Length].position;
            SpawnItem(prefab, pos, "CheatBanishment");
            Debug.Log("spawn banihsment cheat : " + item);
            idx++;
        }

        Debug.Log($"[ItemSpawner] CheatSpawnBanishment: {idx} item dispawn");
    }

    private void SpawnItem(GameObject prefab, Vector3 pos, string label = "")
    {
        if (prefab == null) return;
        var spawnPos = pos + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        var obj = Instantiate(prefab, spawnPos, Quaternion.identity);
        var net = obj.GetComponent<NetworkObject>();
        if (net != null)
        {
            net.Spawn(true);
            Debug.Log($"[ItemSpawner] {label} Spawn: {prefab.name} di {pos}");
        }
        else Debug.LogWarning($"[ItemSpawner] {prefab.name} ga ad NetworkObject");
    }

    private GameObject GetPrefabByType(ItemType type)
    {
        foreach (var p in allItemPrefabs)
        {
            if (p == null) continue;
            var pickup = p.GetComponent<ItemPickUp>();
            if (pickup != null && pickup.itemType == type) return p;
        }
        return null;
    }

    public GhostType[] GetActiveGhosts()
        => GameRecipeRegistry.Instance?.GetActiveGhosts() ?? new GhostType[0];

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}