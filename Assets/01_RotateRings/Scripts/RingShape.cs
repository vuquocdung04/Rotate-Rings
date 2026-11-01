using UnityEngine;

namespace RotateRings
{
    /// <summary>Authoring helper: writes a ring (C-shape, or closed when gapAngle = 0) into the Piece's path.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Piece))]
    public class RingShape : MonoBehaviour
    {
        public float radius = 1.25f;
        [Tooltip("Visible gap between the cap tips, degrees. 0 = closed ring.")]
        [Range(0f, 180f)] public float gapAngle = 70f;
        [Tooltip("Local angle of the gap center, degrees (90 = up).")]
        public float gapCenter = 90f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var piece = GetComponent<Piece>();
            if (piece == null) return;
            piece.SetShape(PathShape.Ring(radius, gapCenter, gapAngle, piece.thickness));
        }
    }
}
