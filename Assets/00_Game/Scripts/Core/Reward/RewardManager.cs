using System;
using System.Collections.Generic;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance { get; private set; }

    [SerializeField] private RewardCatalogSO catalog;

    private readonly Dictionary<string, Action<GameReward>> _binds = new();
    private readonly Dictionary<string, Func<bool>> _owned = new();

    public RewardCatalogSO Catalog => catalog;

    public IReadOnlyCollection<string> BoundIds => _binds.Keys;

    public event Action<GameReward> OnGrant;

    public void Init()
    {
        Instance = this;
        _binds.Clear();
        _owned.Clear();
        EnsureCatalog();
    }

    private void EnsureCatalog()
    {
        if (catalog != null) return;

        catalog = Resources.Load<RewardCatalogSO>(RewardCatalogSO.RESOURCE_PATH);

        if (catalog == null)
            Debug.LogError($"[Reward] Khong tim thay RewardCatalogSO trong Resources/{RewardCatalogSO.RESOURCE_PATH}.");
    }

    public void Bind(string id, Action<GameReward> handler)
    {
        if (string.IsNullOrEmpty(id) || handler == null) return;

        if (_binds.ContainsKey(id))
            Debug.LogError($"[Reward] id '{id}' da Bind roi, lan sau se ghi de len lan truoc.");

        _binds[id] = handler;
    }

    public bool IsBound(string id) => !string.IsNullOrEmpty(id) && _binds.ContainsKey(id);

    public void BindOwned(string id, Func<bool> check)
    {
        if (string.IsNullOrEmpty(id) || check == null) return;

        _owned[id] = check;
    }

    public bool IsOwned(string id)
        => !string.IsNullOrEmpty(id) && _owned.TryGetValue(id, out Func<bool> check) && check();

    public bool IsFlag(string id)
    {
        RewardEntry entry = GetEntry(id);
        return entry != null && entry.category == RewardCategory.Flag;
    }

    public void Grant(string id, int quantity) => Grant(new GameReward(id, quantity));

    public void Grant(GameReward reward)
    {
        if (reward == null || string.IsNullOrEmpty(reward.id)) return;

        if (_binds.TryGetValue(reward.id, out Action<GameReward> handler)) handler(reward);
        else Debug.LogError($"[Reward] id '{reward.id}' chua Bind trong RewardBindings — khong cong duoc gi.");

        OnGrant?.Invoke(reward);
    }

    public void Grant(IReadOnlyList<GameReward> rewards)
    {
        if (rewards == null) return;

        for (int i = 0; i < rewards.Count; i++) Grant(rewards[i]);
    }

    public Awaitable ShowAsync(string id, int quantity)
        => ShowAsync(new List<GameReward> { new GameReward(id, quantity) });

    public async Awaitable ShowAsync(IReadOnlyList<GameReward> rewards)
    {
        if (rewards == null || rewards.Count == 0) return;

        await PopupManager.Show<RewardBox>(box => box.Populate(rewards));
    }

    public Awaitable ClaimAsync(string id, int quantity)
        => ClaimAsync(new List<GameReward> { new GameReward(id, quantity) });

    public async Awaitable ClaimAsync(IReadOnlyList<GameReward> rewards)
    {
        Grant(rewards);
        await ShowAsync(rewards);
    }

    public Sprite GetIcon(string id) => catalog != null ? catalog.GetIcon(id) : null;

    public RewardEntry GetEntry(string id) => catalog != null ? catalog.Find(id) : null;

    public string Format(string id, int quantity)
        => catalog != null ? catalog.Format(id, quantity) : quantity.ToString();
}
