using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[System.Serializable]
public class AudioEntry
{
    public string key;

    public List<AudioClip> variants;

    [TableColumnWidth(70)]
    [Range(0f, 1f)] public float volume = 1f;

    [TableColumnWidth(70)]
    [Range(0.5f, 2f)] public float minPitch = 0.7f;

    [TableColumnWidth(70)]
    [Range(0.5f, 2f)] public float maxPitch = 1.1f;

    public AudioClip GetRandomClip()
    {
        if (variants == null || variants.Count == 0)
            return null;

        int rand = Random.Range(0, variants.Count);
        return variants[rand];
    }

    public float GetRandomPitch()
    {
        return Random.Range(minPitch, maxPitch);
    }
}

[CreateAssetMenu(fileName = "AudioCatalogSO", menuName = "Base/Audio/Audio Catalog")]
public class AudioCatalogSO : ScriptableObject
{
    [Header("🔊 All Audio Configurations")]
    [InfoBox("Generate Keys nằm ở Modules/Audio.")]
    [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "key")]
    public List<AudioEntry> audioConfigs = new();
}
