// AUTO-GENERATED CODE. DO NOT EDIT.
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SceneSwitcherMenu_Generated
{
    [MenuItem("OpenScene/1. LoadingScene &1", priority = 1)]
    public static void OpenScene1()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/00_Game/Scenes/LoadingScene.unity");
        }
    }

    [MenuItem("OpenScene/2. GamePlayScene &2", priority = 2)]
    public static void OpenScene2()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene("Assets/00_Game/Scenes/GamePlayScene.unity");
        }
    }

}
