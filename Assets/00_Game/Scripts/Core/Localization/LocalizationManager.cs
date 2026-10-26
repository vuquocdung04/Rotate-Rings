using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class LocalizationManager
{
    public const string RESOURCE_PATH = "LocalizationData";
    public const string SIMULATED_DEVICE_LANGUAGE_PREF = "Modules.Localization.SimulatedDeviceLanguage";

    public static event Action LanguageChanged;

    private static readonly Dictionary<string, string> Empty = new();

    private static LocalizationData data;
    private static Dictionary<string, string> current = Empty;
    private static Dictionary<string, string> fallback = Empty;
    private static string language;

#if UNITY_EDITOR
    private static readonly HashSet<string> warnedKeys = new();
#endif

    public static string Language
    {
        get
        {
            EnsureLoaded();
            return language;
        }
    }

    public static IReadOnlyList<string> Languages
    {
        get
        {
            EnsureLoaded();
            return data.languages;
        }
    }

    public static bool HasUserChoice => GamePrefs.Has(StringHelper.LANGUAGE);

    public static SystemLanguage DeviceLanguage
    {
        get
        {
#if UNITY_EDITOR
            string simulated = UnityEditor.EditorPrefs.GetString(SIMULATED_DEVICE_LANGUAGE_PREF, string.Empty);
            if (Enum.TryParse(simulated, out SystemLanguage parsed)) return parsed;
#endif
            return Application.systemLanguage;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
        current = Empty;
        fallback = Empty;
        language = null;
        LanguageChanged = null;
#if UNITY_EDITOR
        warnedKeys.Clear();
#endif
    }

    // ========== PUBLIC API ==========

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        EnsureLoaded();

        if (current.TryGetValue(key, out string value)) return value;

#if UNITY_EDITOR
        if (warnedKeys.Add($"{language}/{key}"))
            Debug.LogWarning($"[Localization] Thiếu '{key}' ({language}).");
#endif

        return fallback.TryGetValue(key, out value) ? value : key;
    }

    public static string Get(string key, params object[] args)
    {
        string pattern = Get(key);
        if (args == null || args.Length == 0) return pattern;

        try
        {
            return string.Format(pattern, args);
        }
        catch (FormatException e)
        {
            Debug.LogError($"[Localization] Format sai ở '{key}' ({language}): {e.Message}");
            return pattern;
        }
    }

    public static string GetIn(string targetLanguage, string key)
    {
        EnsureLoaded();

        return data.texts.TryGetValue(targetLanguage, out Dictionary<string, string> table)
               && table.TryGetValue(key, out string value)
            ? value
            : Get(key);
    }

    public static bool HasKey(string key)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(key) && (current.ContainsKey(key) || fallback.ContainsKey(key));
    }

    public static bool Supports(string targetLanguage)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(targetLanguage) && data.texts.ContainsKey(targetLanguage);
    }

    public static void SetLanguage(string targetLanguage)
    {
        if (!Supports(targetLanguage))
        {
            Debug.LogWarning($"[Localization] Không có ngôn ngữ '{targetLanguage}'.");
            return;
        }

        GamePrefs.Set(StringHelper.LANGUAGE, targetLanguage);

        if (targetLanguage != language) Apply(targetLanguage);
    }

    public static void ClearUserChoice()
    {
        GamePrefs.Delete(StringHelper.LANGUAGE);
        GamePrefs.Flush();

        if (data == null) return;

        string resolved = ResolveStartLanguage();
        if (resolved != language) Apply(resolved);
    }

    public static string FromSystemLanguage(SystemLanguage systemLanguage)
    {
        EnsureLoaded();

        string name = systemLanguage.ToString();
        if (data.texts.ContainsKey(name)) return name;

        switch (systemLanguage)
        {
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
                return FirstSupported(nameof(SystemLanguage.Chinese));
            case SystemLanguage.Chinese:
                return FirstSupported(nameof(SystemLanguage.ChineseSimplified), nameof(SystemLanguage.ChineseTraditional));
            default:
                return null;
        }
    }

    // ========== INTERNAL ==========

    private static void EnsureLoaded()
    {
        if (data != null) return;

        data = Load();
        fallback = Table(data.fallback);
        Apply(ResolveStartLanguage());
    }

    private static LocalizationData Load()
    {
        TextAsset asset = Resources.Load<TextAsset>(RESOURCE_PATH);
        if (asset == null)
        {
            Debug.LogWarning($"[Localization] Không tìm thấy Resources/{RESOURCE_PATH}.json — mở Modules/Localization để tải sheet.");
            return new LocalizationData();
        }

        try
        {
            return JsonConvert.DeserializeObject<LocalizationData>(asset.text) ?? new LocalizationData();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Localization] Không đọc được {RESOURCE_PATH}.json: {e.Message}");
            return new LocalizationData();
        }
        finally
        {
            Resources.UnloadAsset(asset);
        }
    }

    private static string ResolveStartLanguage()
    {
        string saved = GamePrefs.Get<string>(StringHelper.LANGUAGE);
        if (!string.IsNullOrEmpty(saved) && data.texts.ContainsKey(saved)) return saved;

        return FromSystemLanguage(DeviceLanguage) ?? data.fallback;
    }

    private static void Apply(string targetLanguage)
    {
        language = targetLanguage;
        current = Table(targetLanguage);
        LanguageChanged?.Invoke();
    }

    private static Dictionary<string, string> Table(string targetLanguage)
    {
        return !string.IsNullOrEmpty(targetLanguage) && data.texts.TryGetValue(targetLanguage, out Dictionary<string, string> table)
            ? table
            : Empty;
    }

    private static string FirstSupported(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            if (data.texts.ContainsKey(candidate)) return candidate;
        }

        return null;
    }
}
