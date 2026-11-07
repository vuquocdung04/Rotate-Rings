using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Tools > Rotate Rings > Game Config.
    /// Picks the level GamePlayScene builds on Play: edits the Testing fields of GameConfigSO, the same asset the game
    /// reads. Testing only applies in the Editor; builds always play the saved progress.
    /// </summary>
    public class GameConfigWindow : OdinEditorWindow
    {
        const string GameScenePath = "Assets/00_Game/Scenes/GamePlayScene.unity";
        const string LevelsDir = "Assets/01_RotateRings/Resources/" + LevelLoader.ResourceDir;
        const float ButtonH = 26f, StepW = 28f;

        [System.NonSerialized] SerializedObject config;

        [MenuItem("Tools/Rotate Rings/Game Config")]
        static void Open()
        {
            var w = GetWindow<GameConfigWindow>("Game Config");
            w.minSize = new Vector2(320f, 240f);
        }

        [OnInspectorGUI]
        void Draw()
        {
            config ??= new SerializedObject(GameConfigSO.Instance);
            config.Update();
            var testing = config.FindProperty("testing");
            var levelTest = config.FindProperty("levelTest");

            EditorGUILayout.PropertyField(testing, new GUIContent("Use Test Level", "Off: plays the saved progress."));
            DrawLevelField(levelTest);

            if (config.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(GameConfigSO.Instance);

            EditorGUILayout.Space(6f);
            int current = GameConfigSO.Instance.CurrentLevel;
            var json = LevelLoader.FindLevel(current);
            DrawStatus(current, json);
            DrawPlayButton(json);
        }

        static void DrawLevelField(SerializedProperty levelTest)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Level");
                if (GUILayout.Button("-", GUILayout.Width(StepW))) levelTest.intValue--;
                levelTest.intValue = EditorGUILayout.IntField(levelTest.intValue);
                if (GUILayout.Button("+", GUILayout.Width(StepW))) levelTest.intValue++;
            }
            levelTest.intValue = Mathf.Max(1, levelTest.intValue);
        }

        static void DrawStatus(int current, TextAsset json)
        {
            string source = GameConfigSO.Instance.Testing ? "Test level" : "Saved progress";
            if (json == null)
            {
                EditorGUILayout.HelpBox($"{source}: level {current} has no file in {LevelsDir}.", MessageType.Warning);
                return;
            }
            var level = JsonUtility.FromJson<LevelData>(json.text);
            EditorGUILayout.HelpBox($"{source}: Play builds {json.name}  ·  {level.pieces.Count} pieces, " +
                                    $"{level.links.Count} links, {level.arms.Count} arms, {level.clamps.Count} clamps.",
                MessageType.Info);
        }

        static void DrawPlayButton(TextAsset json)
        {
            bool playing = EditorApplication.isPlaying;
            using (new EditorGUI.DisabledScope(json == null || EditorApplication.isCompiling))
            {
                if (GUILayout.Button(playing ? "Reload Level" : "Play", GUILayout.Height(ButtonH)))
                {
                    if (playing) ReloadLevel();
                    else PlayGameScene();
                }
            }
        }

        static void ReloadLevel()
        {
            var loader = FindFirstObjectByType<LevelLoader>();
            if (loader != null) loader.LoadCurrent();
            else Debug.LogWarning("[GameConfig] No LevelLoader in the open scene.");
        }

        static void PlayGameScene()
        {
            if (SceneManager.GetActiveScene().path != GameScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(GameScenePath);
            }
            EditorApplication.isPlaying = true;
        }
    }
}
