using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Authoring helper: writes a pipe (I, L, U, S... polyline with rounded corners) into the Piece's path.
    /// Pipes don't rotate: they slide along the straight segment held by a PipeClamp or a link sleeve.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Piece))]
    public class PipeShape : MonoBehaviour
    {
        [Tooltip("Corner points in local space.")]
        public List<Vector2> points = new() { new Vector2(0f, -1f), new Vector2(0f, 1f) };
        [Tooltip("Keep >= the piece thickness so the inner side of a bend doesn't fold.")]
        public float cornerRadius = 0.4f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var piece = GetComponent<Piece>();
            if (piece == null) return;
            piece.SetShape(PathShape.Polyline(points, cornerRadius));
        }
    }
}
