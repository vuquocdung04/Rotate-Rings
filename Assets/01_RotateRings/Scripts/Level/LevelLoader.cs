using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Runtime: builds a level from JSON under this object on Start, and optionally fits the camera to it.
    /// Skips building if pieces are already here (e.g. a level opened in the editor tool).
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        public TextAsset levelJson;
        public PieceStyle style;
        public Camera fitCamera;
        [Tooltip("World units of empty space kept around the level when fitting the camera.")]
        public float margin = 0.8f;

        void Start()
        {
            if (levelJson == null || style == null) return;
            if (GetComponentInChildren<Piece>(true) != null) return;
            Load(JsonUtility.FromJson<LevelData>(levelJson.text));
        }

        public void Load(LevelData level)
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
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
