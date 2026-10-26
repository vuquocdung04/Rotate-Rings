using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class LocalizationFontFallback
{
    private static readonly (string Family, string Language)[] Candidates =
#if UNITY_ANDROID && !UNITY_EDITOR
    {
        ("Noto Sans CJK JP", "Japanese"),
        ("Noto Sans CJK SC", "Chinese"),
        ("Noto Sans CJK KR", "Korean"),
        ("Droid Sans Fallback", null),
        ("Noto Sans Thai", "Thai"),
        ("Noto Naskh Arabic", "Arabic"),
        ("Roboto", null),
    };
#elif (UNITY_IOS && !UNITY_EDITOR) || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    {
        ("Hiragino Sans", "Japanese"),
        ("PingFang SC", "Chinese"),
        ("Apple SD Gothic Neo", "Korean"),
        ("Thonburi", "Thai"),
        ("Geeza Pro", "Arabic"),
        ("Helvetica Neue", null),
    };
#else
    {
        ("Yu Gothic", "Japanese"),
        ("Microsoft YaHei", "Chinese"),
        ("Malgun Gothic", "Korean"),
        ("Leelawadee UI", "Thai"),
        ("Segoe UI", "Arabic"),
    };
#endif

    private static readonly List<(TMP_FontAsset Font, string Language)> created = new();
    private static bool createdList;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (created.Count > 0) return;

        foreach ((string family, string language) in Candidates)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(family, null);
            if (font == null) continue;

            font.name = $"{family} (OS)";
            created.Add((font, language));
        }

        if (created.Count == 0)
        {
            Debug.LogWarning("[Localization] Không tìm thấy font hệ thống nào để làm fallback cho TMP.");
            return;
        }

        if (TMP_Settings.fallbackFontAssets == null)
        {
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
            createdList = true;
        }

        Apply();
        LocalizationManager.LanguageChanged += Apply;
        Application.quitting += Uninstall;
    }

    private static void Apply()
    {
        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
        string language = LocalizationManager.Language ?? string.Empty;

        foreach ((TMP_FontAsset font, string _) in created) fallbacks.Remove(font);

        foreach ((TMP_FontAsset font, string tag) in created)
        {
            if (Matches(tag, language)) fallbacks.Add(font);
        }

        foreach ((TMP_FontAsset font, string tag) in created)
        {
            if (!Matches(tag, language)) fallbacks.Add(font);
        }
    }

    private static void Uninstall()
    {
        LocalizationManager.LanguageChanged -= Apply;
        Application.quitting -= Uninstall;

        List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;

        foreach ((TMP_FontAsset font, string _) in created)
        {
            fallbacks?.Remove(font);
            if (font == null) continue;

            foreach (Texture2D atlas in font.atlasTextures) Object.Destroy(atlas);
            Object.Destroy(font.material);
            Object.Destroy(font);
        }

        if (createdList) TMP_Settings.fallbackFontAssets = null;

        created.Clear();
        createdList = false;
    }

    private static bool Matches(string tag, string language)
    {
        return !string.IsNullOrEmpty(tag) && language.StartsWith(tag);
    }
}
