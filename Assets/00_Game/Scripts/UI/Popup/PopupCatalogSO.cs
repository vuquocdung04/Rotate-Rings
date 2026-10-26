using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PopupEntry
{
    public string typeName;

    public bool preload;
    public float animDuration = 0.3f;
    [Range(0f, 1f)] public float backdropAlpha = 0.95f;
    public bool closeOnBackdropClick;
    public bool peekOnBackdropHold;
    public float peekFadeDuration = 0.15f;

    public static readonly PopupEntry Fallback = new PopupEntry();
}

[CreateAssetMenu(menuName = "Base/UI/Popup Catalog", fileName = "PopupCatalogSO")]
public class PopupCatalogSO : ScriptableObject
{
    public const string RESOURCE_PATH = "PopupCatalogSO";

    public List<PopupEntry> popups = new();

    public PopupEntry Find(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;

        for (int i = 0; i < popups.Count; i++)
        {
            if (popups[i] != null && popups[i].typeName == typeName) return popups[i];
        }
        return null;
    }
}
