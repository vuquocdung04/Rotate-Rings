using Core.GameUI.Util;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Runtime: builds a level from JSON under this object on Start, and optionally fits the camera to it.
    /// The level comes from GameConfigSO.CurrentLevel unless levelJson forces a file.
    /// Skips building if pieces are already here (e.g. a level opened in the editor tool).
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        public const string ResourceDir = "Levels";

        [Tooltip("Always plays this file. Empty = the current level from GameConfigSO.")]
        public TextAsset levelJson;
        public PieceStyle style;
        public Camera fitCamera;
        [Tooltip("Part of the screen the level must fit in. Empty = the whole screen.")]
        public ScreenArea area;
        [Tooltip("World units of empty space kept around the level when fitting the camera.")]
        public float margin = 0.8f;
        [Tooltip("Small levels stop zooming in here, so rings keep a similar size from level to level.")]
        public float minOrthographicSize = 5f;

        /// <summary>Level N is the file Resources/Levels/level_00N.json; null if it does not exist.</summary>
        public static TextAsset FindLevel(int number) => Resources.Load<TextAsset>($"{ResourceDir}/level_{number:000}");

        void OnEnable()
        {
            if (area != null) area.Changed += FitCamera;
        }

        void OnDisable()
        {
            if (area != null) area.Changed -= FitCamera;
        }

        void Start()
        {
            if (GetComponentInChildren<Piece>(true) != null) return;
            LoadCurrent();
        }

        public void LoadCurrent()
        {
            int number = GameConfigSO.Instance.CurrentLevel;
            var json = levelJson != null ? levelJson : FindLevel(number);
            if (json == null || style == null)
            {
                Debug.LogError(style == null
                    ? "[LevelLoader] Piece style is not assigned."
                    : $"[LevelLoader] Level {number} not found in Resources/{ResourceDir}.", this);
                return;
            }
            Load(JsonUtility.FromJson<LevelData>(json.text));
        }

        public void Load(LevelData level)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                child.SetParent(null); // Destroy waits for the frame's end: FitCamera must not measure the old level
                Destroy(child.gameObject);
            }
            LevelBuilder.Build(level, style, transform);
            if (fitCamera != null) FitCamera();
        }

        /// <summary>
        /// Zooms and moves the orthographic camera so the pieces sit in the middle of the area, with a margin.
        /// The camera moves, not the level: collisions and link sizes are in world units and would not survive scaling.
        /// </summary>
        public void FitCamera()
        {
            if (fitCamera == null) return;
            var renderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            Rect view = area != null ? area.ViewportRect() : new Rect(0f, 0f, 1f, 1f);
            if (view.width <= 0f || view.height <= 0f) return;

            float aspect = Mathf.Max(0.01f, fitCamera.aspect);
            float size = Mathf.Max(
                (b.extents.y + margin) / view.height,
                (b.extents.x + margin) / (view.width * aspect),
                minOrthographicSize);
            fitCamera.orthographicSize = size;

            // How far the area's center sits from the screen's, in world units at this zoom.
            float offsetX = (view.center.x - 0.5f) * 2f * size * aspect;
            float offsetY = (view.center.y - 0.5f) * 2f * size;
            var cp = fitCamera.transform.position;
            fitCamera.transform.position = new Vector3(b.center.x - offsetX, b.center.y - offsetY, cp.z);
        }
    }
}
