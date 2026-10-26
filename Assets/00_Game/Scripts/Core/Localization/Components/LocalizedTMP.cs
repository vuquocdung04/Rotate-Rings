using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMP : MonoBehaviour
{
    [SerializeField, ValueDropdown(nameof(Keys), AppendNextDrawer = true)]
    private string key;

    [SerializeField] private bool upperCase;

    private TMP_Text label;
    private object[] args;

    public string Key => key;

    private TMP_Text Label
    {
        get
        {
            if (label == null) label = GetComponent<TMP_Text>();
            return label;
        }
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= Refresh;
    }

    public void SetKey(string newKey, params object[] newArgs)
    {
        key = newKey;
        args = newArgs;
        Refresh();
    }

    public void SetArgs(params object[] newArgs)
    {
        args = newArgs;
        Refresh();
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(key)) return;

        string text = LocalizationManager.Get(key, args);
        Label.text = upperCase ? text.ToUpperInvariant() : text;
    }

    private static IEnumerable<string> Keys() => LocalizationKeys.All;
}
