using System;
using UnityEngine;

namespace Core.GameUI.Util
{
    /// <summary>
    /// A band of the screen given in percents of the safe area, so it holds on any screen size and stays clear of
    /// notches. Select the object to drag its edges in the Scene view. Raises Changed when the screen or the band changes.
    /// </summary>
    public class ScreenArea : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool useSafeArea = true;
        [SerializeField, Range(0f, 1f)] private float widthPercent = 0.9f;
        [SerializeField, Range(0f, 1f)] private float bottomPercent = 0f;
        [SerializeField, Range(0f, 1f)] private float topPercent = 1f;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField, Range(0f, 0.5f)] private float gizmoFillAlpha = 0.12f;

        private Camera cameraCache;
        private Vector2Int lastScreenSize;
        private Rect lastSafeArea;

        public event Action Changed;

        public Bounds WorldBounds => BoundsFrom(ViewWorldRect());

        /// <summary>The band in viewport space (0..1): independent of where the camera sits or how far it is zoomed.</summary>
        public Rect ViewportRect()
        {
            Rect view = useSafeArea ? SafeViewport() : new Rect(0f, 0f, 1f, 1f);

            float min = Mathf.Min(bottomPercent, topPercent);
            float max = Mathf.Max(bottomPercent, topPercent);
            float width = view.width * widthPercent;

            return Rect.MinMaxRect(
                view.center.x - width * 0.5f,
                view.yMin + view.height * min,
                view.center.x + width * 0.5f,
                view.yMin + view.height * max);
        }

        public Rect ViewWorldRect()
        {
            return useSafeArea ? SafeWorldRect() : CameraWorldRect();
        }

        private Bounds BoundsFrom(Rect view)
        {
            if (view.width <= 0f || view.height <= 0f)
                return new Bounds();

            float min = Mathf.Min(bottomPercent, topPercent);
            float max = Mathf.Max(bottomPercent, topPercent);

            float bottom = view.yMin + view.height * min;
            float top = view.yMin + view.height * max;

            Vector3 center = new Vector3(view.center.x, (bottom + top) * 0.5f, transform.position.z);
            Vector3 size = new Vector3(view.width * widthPercent, top - bottom, 0f);

            return new Bounds(center, size);
        }

        private Rect CameraWorldRect()
        {
            return ViewportToWorldRect(Vector2.zero, Vector2.one);
        }

        private Rect SafeWorldRect()
        {
            Rect safe = SafeViewport();
            return ViewportToWorldRect(safe.min, safe.max);
        }

        private Rect SafeViewport()
        {
            Rect full = new Rect(0f, 0f, 1f, 1f);
            Vector2 screen = ScreenSize();
            if (screen.x <= 0f || screen.y <= 0f)
                return full;

            Rect safeArea = Screen.safeArea;

            Vector2 min = new Vector2(Mathf.Clamp01(safeArea.xMin / screen.x), Mathf.Clamp01(safeArea.yMin / screen.y));
            Vector2 max = new Vector2(Mathf.Clamp01(safeArea.xMax / screen.x), Mathf.Clamp01(safeArea.yMax / screen.y));

            if (max.x - min.x <= 0f || max.y - min.y <= 0f)
                return full;

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Rect ViewportToWorldRect(Vector2 min, Vector2 max)
        {
            Camera cam = ResolveCamera();
            if (cam == null)
                return Rect.zero;

            float distance = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);

            Vector3 worldMin = cam.ViewportToWorldPoint(new Vector3(min.x, min.y, distance));
            Vector3 worldMax = cam.ViewportToWorldPoint(new Vector3(max.x, max.y, distance));

            return Rect.MinMaxRect(
                Mathf.Min(worldMin.x, worldMax.x),
                Mathf.Min(worldMin.y, worldMax.y),
                Mathf.Max(worldMin.x, worldMax.x),
                Mathf.Max(worldMin.y, worldMax.y));
        }

        private Vector2 ScreenSize()
        {
#if UNITY_EDITOR
            Vector2 gameView = UnityEditor.Handles.GetMainGameViewSize();

            if (gameView.x > 0f && gameView.y > 0f)
                return gameView;
#endif
            return new Vector2(Screen.width, Screen.height);
        }

        private void Awake()
        {
            CaptureScreen();
        }

        private void Update()
        {
            if (Screen.width == lastScreenSize.x && Screen.height == lastScreenSize.y && Screen.safeArea == lastSafeArea)
                return;

            CaptureScreen();

            Changed?.Invoke();
        }

        // Dragging the band in the Scene view during Play refits right away.
        private void OnValidate()
        {
            if (Application.isPlaying)
                Changed?.Invoke();
        }

        private void CaptureScreen()
        {
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null)
                return targetCamera;

            if (cameraCache == null)
                cameraCache = Camera.main;

            return cameraCache;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            Bounds bounds = WorldBounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
                return;

            Vector3 size = new Vector3(bounds.size.x, bounds.size.y, 0f);

            if (gizmoFillAlpha > 0f)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoFillAlpha);
                Gizmos.DrawCube(bounds.center, size);
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(bounds.center, size);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            DrawRect(CameraWorldRect(), new Color(1f, 1f, 1f, 0.4f));

            if (useSafeArea)
                DrawRect(SafeWorldRect(), new Color(1f, 0.92f, 0.2f, 0.9f));
        }

        private void DrawRect(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(new Vector3(rect.center.x, rect.center.y, transform.position.z), new Vector3(rect.width, rect.height, 0f));
        }
    }
}
