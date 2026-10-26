using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

public static class GamePrefs
{
    private const string Prefix = "gp.";
    private const string IndexKey = "gp.__keys";
    private const char Separator = '\n';

    private static readonly Dictionary<string, object> cache = new();
    private static readonly HashSet<string> keys = LoadIndex();

    public static IReadOnlyCollection<string> Keys => keys;

    public static T Get<T>(string key, T defaultValue = default)
    {
        if (cache.TryGetValue(key, out object cached)) return cached is T typed ? typed : defaultValue;

        T value = Read(key, defaultValue);
        cache[key] = value;
        return value;
    }

    public static void Set<T>(string key, T value)
    {
        if (IsSimple(typeof(T)) && cache.TryGetValue(key, out object cached) && Equals(cached, value)) return;

        cache[key] = value;
        PlayerPrefs.SetString(Prefix + key, Encode(value));

        if (keys.Add(key)) SaveIndex();
    }

    public static bool Has(string key) => keys.Contains(key);

    public static void Delete(string key)
    {
        cache.Remove(key);
        PlayerPrefs.DeleteKey(Prefix + key);

        if (keys.Remove(key)) SaveIndex();
    }

    public static void ClearAll()
    {
        foreach (string key in keys) PlayerPrefs.DeleteKey(Prefix + key);

        keys.Clear();
        cache.Clear();
        PlayerPrefs.DeleteKey(IndexKey);
        PlayerPrefs.Save();
    }

    public static void Flush() => PlayerPrefs.Save();

    public static string PeekRaw(string key) => PlayerPrefs.GetString(Prefix + key, null);

    // ========== ENCODE / DECODE ==========
    private static T Read<T>(string key, T defaultValue)
    {
        string full = Prefix + key;
        if (!PlayerPrefs.HasKey(full)) return defaultValue;

        string raw = PlayerPrefs.GetString(full);
        Type type = typeof(T);

        try
        {
            if (type == typeof(string)) return (T)(object)raw;
            if (type == typeof(int)) return (T)(object)int.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return (T)(object)long.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return (T)(object)float.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return (T)(object)(raw == "1");
            if (type.IsEnum) return (T)Enum.Parse(type, raw);

            return JsonConvert.DeserializeObject<T>(raw);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GamePrefs] Không đọc được key '{key}': {e.Message}");
            return defaultValue;
        }
    }

    private static string Encode<T>(T value)
    {
        Type type = typeof(T);

        if (type == typeof(string)) return (string)(object)value ?? string.Empty;
        if (type == typeof(int)) return ((int)(object)value).ToString(CultureInfo.InvariantCulture);
        if (type == typeof(long)) return ((long)(object)value).ToString(CultureInfo.InvariantCulture);
        if (type == typeof(float)) return ((float)(object)value).ToString(CultureInfo.InvariantCulture);
        if (type == typeof(bool)) return (bool)(object)value ? "1" : "0";
        if (type.IsEnum) return value.ToString();

        return JsonConvert.SerializeObject(value);
    }

    private static bool IsSimple(Type type)
    {
        return type.IsEnum
               || type == typeof(int)
               || type == typeof(long)
               || type == typeof(float)
               || type == typeof(bool)
               || type == typeof(string);
    }

    // ========== INDEX ==========
    private static HashSet<string> LoadIndex()
    {
        var set = new HashSet<string>();
        string raw = PlayerPrefs.GetString(IndexKey, string.Empty);
        if (string.IsNullOrEmpty(raw)) return set;

        foreach (string key in raw.Split(Separator))
        {
            if (!string.IsNullOrEmpty(key)) set.Add(key);
        }
        return set;
    }

    private static void SaveIndex()
    {
        PlayerPrefs.SetString(IndexKey, string.Join(Separator.ToString(), keys));
    }
}
