using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable, HideReferenceObjectPicker]
public class GameReward
{
    [ValueDropdown(nameof(RewardIds))]
    public string id;

    public int quantity;

    private static RewardCatalogSO _catalogCache;
    private static string[] _idsCache = Array.Empty<string>();

    private static IEnumerable<string> RewardIds()
    {
        RewardCatalogSO catalog = RewardManager.Instance != null ? RewardManager.Instance.Catalog : _catalogCache;

        if (catalog == null)
        {
            catalog = Resources.Load<RewardCatalogSO>(RewardCatalogSO.RESOURCE_PATH);
            if (catalog == null) return _idsCache;
        }

        if (!ReferenceEquals(catalog, _catalogCache) || IsCacheStale(catalog)) RebuildCache(catalog);

        return _idsCache;
    }

    private static bool IsCacheStale(RewardCatalogSO catalog)
    {
        int index = 0;

        for (int i = 0; i < catalog.rewards.Count; i++)
        {
            RewardEntry entry = catalog.rewards[i];
            if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

            if (index >= _idsCache.Length || _idsCache[index] != entry.id) return true;
            index++;
        }

        return index != _idsCache.Length;
    }

    private static void RebuildCache(RewardCatalogSO catalog)
    {
        var ids = new List<string>(catalog.rewards.Count);

        for (int i = 0; i < catalog.rewards.Count; i++)
        {
            RewardEntry entry = catalog.rewards[i];
            if (entry != null && !string.IsNullOrEmpty(entry.id)) ids.Add(entry.id);
        }

        _catalogCache = catalog;
        _idsCache = ids.ToArray();
    }

    public GameReward() { }

    public GameReward(string id, int quantity)
    {
        this.id = id;
        this.quantity = quantity;
    }
}
