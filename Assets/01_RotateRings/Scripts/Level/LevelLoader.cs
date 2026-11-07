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
        [Tooltip("World units of empty space kept around the level when fitting the camera.")]
        public float margin = 0.8f;

        /// <summary>Level N is the file Resources/Levels/level_00N.json; null if it does not exist.</summary>
        public static TextAsset FindLevel(int number) => Resources.Load<TextAsset>($"{ResourceDir}/level_{number:000}");

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

        /// <summary>Centers the orthographic camera on the pieces and zooms so they fit with a margin.</summary>
        public void FitCamera()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            float halfH = b.extents.y + margin;
            float halfW = (b.extents.x + margin) / Mathf.Max(0.01f, fitCamera.aspect);
            fitCamera.orthographicSize = Mathf.Max(halfH, halfW);
            var cp = fitCamera.transform.position;
            fitCamera.transform.position = new Vector3(b.center.x, b.center.y, cp.z);
        }
    }
}
