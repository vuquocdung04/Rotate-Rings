using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public AudioSource asBg;

    [Header("SFX Pooling")]
    public GameObject sfxPrefab;
    public float sfxSpamCooldown = 0.08f;

    [Header("Data")]
    [SerializeField] private List<AudioCatalogSO> audioDataList;

    private Dictionary<string, AudioEntry> audioLookup;
    private Dictionary<string, float> lastPlayTimes = new();
    private float currentSfxVolume = 1f;

    public void Init()
    {
        Instance = this;
        BuildAudioLookup();
        ApplyMusicVolume();
        ApplySoundVolume();
    }

    private void BuildAudioLookup()
    {
        audioLookup = new Dictionary<string, AudioEntry>();

        foreach (var dataBase in audioDataList)
        {
            if (dataBase == null)
            {
                Debug.LogWarning("AudioCatalogSO null trong AudioManager!");
                continue;
            }

            foreach (var config in dataBase.audioConfigs)
            {
                if (string.IsNullOrEmpty(config.key)) continue;

                string lowerKey = config.key.ToLower();
                if (!audioLookup.TryAdd(lowerKey, config))
                    Debug.LogWarning($"AudioKey bị trùng giữa các database: {config.key} (trong {dataBase.name})");
            }
        }
    }
    public void PlaySfx(string key) => PlaySfxInternal(key, null);

    public void PlaySfx(string key, float pitch) => PlaySfxInternal(key, pitch);

    public void PlaySfxProgress(string key, float t)
    {
        if (!TryGetConfig(key, out string lowerKey, out var config)) return;
        float pitch = Mathf.Lerp(config.minPitch, config.maxPitch, Mathf.Clamp01(t));
        PlayResolved(lowerKey, config, pitch);
    }

    private void PlaySfxInternal(string key, float? pitch)
    {
        if (!TryGetConfig(key, out string lowerKey, out var config)) return;
        PlayResolved(lowerKey, config, pitch ?? config.GetRandomPitch());
    }

    private void PlayResolved(string lowerKey, AudioEntry config, float pitch)
    {
        PlayClipInternal(lowerKey, config.GetRandomClip(), pitch, config.volume * currentSfxVolume);
    }

    private bool TryGetConfig(string key, out string lowerKey, out AudioEntry config)
    {
        config = null;
        lowerKey = null;
        if (string.IsNullOrEmpty(key)) return false;

        lowerKey = key.ToLower();
        if (audioLookup.TryGetValue(lowerKey, out config)) return true;

#if UNITY_EDITOR
        Debug.LogWarning($"Không tìm thấy AudioKey SFX: {key}");
#endif
        return false;
    }

    public void PlaySfx(AudioClip clip, float pitch = 1f)
    {
        if (clip == null) return;
        PlayClipInternal(clip.name.ToLower(), clip, pitch, currentSfxVolume);
    }

    private void PlayClipInternal(string throttleKey, AudioClip clip, float pitch, float volume)
    {
        if (!UseProfile.OnSound || clip == null) return;

        if (lastPlayTimes.TryGetValue(throttleKey, out float lastTime) &&
            Time.time - lastTime < sfxSpamCooldown) return;
        lastPlayTimes[throttleKey] = Time.time;

        var sfxObj = SimplePool2.Spawn(sfxPrefab, Vector3.zero, Quaternion.identity);
        if (sfxObj == null) return;

        var source = sfxObj.GetComponent<AudioSource>();
        source.clip = clip;
        source.pitch = pitch;
        source.volume = volume;
        source.Play();

        DespawnAfterPlayAsync(sfxObj, clip.length).Forget();
    }

    private async Awaitable DespawnAfterPlayAsync(GameObject obj, float delay)
    {
        await Awaitable.WaitForSecondsAsync(delay);
        if (obj != null && obj.activeInHierarchy) SimplePool2.Despawn(obj);
    }

    public void PlayMusic(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (!audioLookup.TryGetValue(key.ToLower(), out var config))
        {
            Debug.LogWarning($"Không tìm thấy AudioKey nhạc: {key}");
            return;
        }

        var clip = config.GetRandomClip();
        if (clip == null) return;

        asBg.clip = clip;
        asBg.loop = true;
        asBg.pitch = 1f;
        asBg.Play();
    }
    public void ToggleSound() => SetSound(!UseProfile.OnSound);
    public void ToggleMusic() => SetMusic(!UseProfile.OnMusic);

    public void SetSound(bool on)
    {
        UseProfile.OnSound = on;
        ApplySoundVolume();
    }

    public void SetMusic(bool on)
    {
        UseProfile.OnMusic = on;
        ApplyMusicVolume();
    }

    private void ApplyMusicVolume()
    {
        asBg.volume = UseProfile.OnMusic ? 0.65f : 0f;
    }

    private void ApplySoundVolume()
    {
        currentSfxVolume = UseProfile.OnSound ? 1f : 0f;
    }
}