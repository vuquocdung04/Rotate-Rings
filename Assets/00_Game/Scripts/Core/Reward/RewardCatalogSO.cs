using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public enum RewardDisplayStyle
{
    Number,
    Multiplier,
    Plus,
    PlusMinutes,
    FlagText
}

public enum RewardCategory
{
    Currency,
    Item,
    Booster,
    Flag
}

[Serializable, HideReferenceObjectPicker]
public sealed class RewardEntry
{
    [TableColumnWidth(66, false)]
    [PreviewField(52, ObjectFieldAlignment.Center), HideLabel]
    public Sprite icon;

    [TableColumnWidth(130), HideLabel]
    public string id;

    [TableColumnWidth(100, false), HideLabel]
    public RewardCategory category = RewardCategory.Currency;

    [TableColumnWidth(120, false), HideLabel]
    public RewardDisplayStyle displayStyle = RewardDisplayStyle.Number;

    [TableColumnWidth(85, false), HideLabel]
    public string suffix = "";

    [TableColumnWidth(90, false), HideLabel]
    [ShowInInspector, DisplayAsString(false)]
    public string Preview => RewardCatalogSO.Format(displayStyle, 1000, suffix);
}

[CreateAssetMenu(menuName = "Base/Reward/Reward Catalog", fileName = "RewardCatalogSO")]
public class RewardCatalogSO : ScriptableObject
{
    public const string RESOURCE_PATH = "RewardCatalogSO";

    public List<RewardEntry> rewards = new();

    public RewardEntry Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < rewards.Count; i++)
        {
            if (rewards[i] != null && rewards[i].id == id) return rewards[i];
        }
        return null;
    }

    public Sprite GetIcon(string id) => Find(id)?.icon;

    public string Format(string id, int quantity)
    {
        RewardEntry entry = Find(id);

        return entry == null
            ? quantity.ToString()
            : Format(entry.displayStyle, quantity, entry.suffix);
    }

    public static string Format(RewardDisplayStyle style, int quantity, string suffix)
    {
        suffix ??= string.Empty;

        return style switch
        {
            RewardDisplayStyle.Number => quantity.ToString("N0") + suffix,
            RewardDisplayStyle.Multiplier => $"x{quantity}{suffix}",
            RewardDisplayStyle.Plus => $"+{quantity}{suffix}",
            RewardDisplayStyle.PlusMinutes => $"+{quantity}{(string.IsNullOrEmpty(suffix) ? "m" : suffix)}",
            RewardDisplayStyle.FlagText => suffix,
            _ => quantity.ToString()
        };
    }
}
