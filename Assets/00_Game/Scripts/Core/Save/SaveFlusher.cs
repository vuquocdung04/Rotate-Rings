using UnityEngine;

public static class SaveFlusher
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        Application.focusChanged -= OnFocusChanged;
        Application.focusChanged += OnFocusChanged;
    }

    private static void OnFocusChanged(bool hasFocus)
    {
        if (!hasFocus) GamePrefs.Flush();
    }
}
