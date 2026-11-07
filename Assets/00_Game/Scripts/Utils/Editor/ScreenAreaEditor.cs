using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Core.GameUI.Util
{
    /// <summary>Scene view handles: top and bottom edges, width, and a dot moving the whole band. Ctrl snaps to 1%.</summary>
    [CustomEditor(typeof(ScreenArea))]
    [CanEditMultipleObjects]
    public class ScreenAreaEditor : OdinEditor
    {
        private static readonly Color WidthColor = new Color(0.35f, 0.8f, 1f);
        private static readonly Color MoveColor = new Color(1f, 1f, 1f, 0.9f);

        private void OnSceneGUI()
        {
            ScreenArea area = target as ScreenArea;

            if (area != null)
                DrawHandles(area);
        }

        private static void DrawHandles(ScreenArea area)
        {
            Rect view = area.ViewWorldRect();
            if (view.width <= 0f || view.height <= 0f)
                return;

            Bounds bounds = area.WorldBounds;
            if (bounds.size.x <= 0f)
                return;

            SerializedObject data = new SerializedObject(area);
            data.Update();

            SerializedProperty bottomProp = data.FindProperty("bottomPercent");
            SerializedProperty topProp = data.FindProperty("topPercent");
            SerializedProperty widthProp = data.FindProperty("widthPercent");

            Color color = data.FindProperty("gizmoColor").colorValue;
            float snap = Event.current.control ? 0.01f : 0.0001f;
            float z = bounds.center.z;
            float edgeX = bounds.center.x - bounds.size.x * 0.2f;
            float moveX = bounds.center.x + bounds.size.x * 0.2f;

            EditorGUI.BeginChangeCheck();
            float top = Drag(new Vector3(edgeX, bounds.max.y, z), Vector3.up, color).y;
            if (EditorGUI.EndChangeCheck())
                topProp.floatValue = Mathf.Clamp(ToPercentY(top, view, snap), bottomProp.floatValue, 1f);

            EditorGUI.BeginChangeCheck();
            float bottom = Drag(new Vector3(edgeX, bounds.min.y, z), Vector3.up, color).y;
            if (EditorGUI.EndChangeCheck())
                bottomProp.floatValue = Mathf.Clamp(ToPercentY(bottom, view, snap), 0f, topProp.floatValue);

            EditorGUI.BeginChangeCheck();
            float right = Drag(new Vector3(bounds.max.x, bounds.center.y, z), Vector3.right, WidthColor).x;
            if (EditorGUI.EndChangeCheck())
                widthProp.floatValue = Mathf.Clamp(Snap((right - view.center.x) * 2f / view.width, snap), 0.05f, 1f);

            EditorGUI.BeginChangeCheck();
            float move = Drag(new Vector3(moveX, bounds.center.y, z), Vector3.up, MoveColor).y;
            if (EditorGUI.EndChangeCheck())
            {
                float half = (topProp.floatValue - bottomProp.floatValue) * 0.5f;
                float mid = Mathf.Clamp(ToPercentY(move, view, snap), half, 1f - half);

                bottomProp.floatValue = Snap(mid - half, snap);
                topProp.floatValue = Snap(mid + half, snap);
            }

            data.ApplyModifiedProperties();
        }

        private static Vector3 Drag(Vector3 position, Vector3 direction, Color color)
        {
            Handles.color = color;

            float size = HandleUtility.GetHandleSize(position) * 0.09f;

            return Handles.Slider(position, direction, size, Handles.DotHandleCap, 0f);
        }

        private static float ToPercentY(float worldY, Rect view, float snap)
        {
            return Mathf.Clamp01(Snap((worldY - view.yMin) / view.height, snap));
        }

        private static float Snap(float value, float snap)
        {
            return Mathf.Round(value / snap) * snap;
        }
    }
}
