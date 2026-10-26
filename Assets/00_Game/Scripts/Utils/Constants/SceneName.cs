using System;
using UnityEngine.SceneManagement;

public static class SceneName
{
    public const string GAME_PLAY = "GamePlayScene";
    public const string LOADING_SCENE = "LoadingScene";
}

public static class SceneUtils
{
    private static string currentScene => SceneManager.GetActiveScene().name;
    public static void ExecuteInScene(string targetScene, Action callback)
    {
        if (currentScene.Equals(targetScene))
        {
            callback?.Invoke();
        }
    }
}