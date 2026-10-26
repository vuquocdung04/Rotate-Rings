using System.IO;
using System.Text;
using UnityEditor;

[InitializeOnLoad]
public class SceneSwitcherWindow
{
    // File script phụ sẽ được tự động sinh ra để tạo menu dropdown
    private const string GENERATED_SCRIPT_PATH = "Assets/Editor/SceneSwitcherMenu_Generated.cs";

    static SceneSwitcherWindow()
    {
        // Tự động cập nhật menu mỗi khi bạn thêm/sửa/xóa Scene trong Build Settings
        EditorBuildSettings.sceneListChanged += GenerateMenuScript;
    }

    [MenuItem("OpenScene/🔄 Refresh Scene List", priority = 0)]
    public static void GenerateMenuScript()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("// AUTO-GENERATED CODE. DO NOT EDIT.");
        sb.AppendLine("using UnityEditor;");
        sb.AppendLine("using UnityEditor.SceneManagement;");
        sb.AppendLine("");
        sb.AppendLine("public static class SceneSwitcherMenu_Generated");
        sb.AppendLine("{");

        int index = 1;
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                string sceneName = Path.GetFileNameWithoutExtension(scene.path);
                
                // Gắn phím tắt Alt + 1, 2, 3...
                string shortcut = index <= 9 ? $" &{index}" : "";
                string menuPath = $"OpenScene/{index}. {sceneName}{shortcut}";
                
                sb.AppendLine($"    [MenuItem(\"{menuPath}\", priority = {index})]");
                sb.AppendLine($"    public static void OpenScene{index}()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())");
                sb.AppendLine("        {");
                sb.AppendLine($"            EditorSceneManager.OpenScene(\"{scene.path}\");");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("");
                index++;
            }
        }

        sb.AppendLine("}");

        // Đảm bảo thư mục Editor tồn tại rồi mới ghi file
        Directory.CreateDirectory(Path.GetDirectoryName(GENERATED_SCRIPT_PATH));
        File.WriteAllText(GENERATED_SCRIPT_PATH, sb.ToString());
        
        AssetDatabase.Refresh();
    }
}