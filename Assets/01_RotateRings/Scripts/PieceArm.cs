using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// A rigid straight rod sticking out of a piece (must be a child object of it, so it moves with it).
    /// It holds nothing; it only collides, blocking the piece against others.
    /// On a ring it points out radially at <see cref="angle"/>; on a pipe it leaves the path at
    /// <see cref="pathT"/> along the normal on <see cref="side"/>.
    /// Drawn with the piece's own material and color, rooted under its body.
    /// </summary>
    [ExecuteAlways]
    public class PieceArm : MonoBehaviour
    {
        [Tooltip("Rings: local angle, degrees.")]
        public float angle = -45f;
        [Tooltip("Pipes: position along the path, 0 = start, 1 = end.")]
        [Range(0f, 1f)] public float pathT = 0.5f;
        [Tooltip("Pipes: +1 = right of the path direction, -1 = left.")]
        public float side = 1f;
        [Tooltip("Length from the body's outer edge to the rod's centerline end.")]
        public float length = 0.9f;

        public Piece Parent { get; private set; }

        MeshRenderer body, shadow, outline;
        Mesh mesh, outlineMesh;
        readonly PathShape rod = new();
        bool outlineOn;
        bool dirty = true;

        void OnEnable()
        {
            Parent = GetComponentInParent<Piece>();
            if (Parent != null && !Parent.Arms.Contains(this)) Parent.Arms.Add(this);
            dirty = true;
        }

        void Update()
        {
            if (dirty) Layout();
        }

        void OnDisable()
        {
            if (Parent != null) Parent.Arms.Remove(this);
        }

        void OnDestroy()
        {
            DestroyMesh(mesh);
            DestroyMesh(outlineMesh);
        }

        static void DestroyMesh(Mesh m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m);
            else DestroyImmediate(m);
        }

        void OnValidate() => dirty = true;

        void LateUpdate()
        {
            if (shadow != null && Parent != null)
                shadow.transform.position = transform.position + (Vector3)Parent.shadowOffset + new Vector3(0f, 0f, 0.01f);
        }

        /// <summary>Rod from the body's centerline outward; the root is hidden under the body.</summary>
        public void Layout()
        {
            dirty = false;
            if (Parent == null) Parent = GetComponentInParent<Piece>();
            if (Parent == null || Parent.shape == null || Parent.shape.segments.Count == 0) return;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            Vector2 root, dir;
            if (Parent.shape.TryGetRingCircle(out Vector2 c, out float r))
            {
                float a = angle * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                root = c + dir * r;
            }
            else
            {
                root = Parent.shape.Evaluate(pathT * Parent.shape.Length, out Vector2 tangent);
                dir = new Vector2(tangent.y, -tangent.x) * Mathf.Sign(side == 0f ? 1f : side);
            }
            rod.closed = false;
            rod.segments.Clear();
            rod.segments.Add(PathSegment.Line(root, root + dir * (Parent.thickness * 0.5f + length)));

            body = GetOrCreatePart("Body", ref body);
            shadow = GetOrCreatePart("Shadow", ref shadow);
            outline = GetOrCreatePart("Outline", ref outline);
            if (mesh == null) mesh = new Mesh { name = "Arm", hideFlags = HideFlags.DontSave };
            if (outlineMesh == null) outlineMesh = new Mesh { name = "ArmOutline", hideFlags = HideFlags.DontSave };

            PathMesh.Build(mesh, rod, Parent.thickness, Parent.styleRow, Piece.ToVertexColor(Parent.color));
            PathMesh.Build(outlineMesh, rod, Parent.thickness + 2f * Parent.outlineWidth, Parent.styleRow,
                Piece.ToVertexColor(Parent.outlineColor));

            Setup(body, mesh, Parent.bodyMaterial, Parent.sortingOrder - 1);
            Setup(shadow, mesh, Parent.shadowMaterial, -1000);
            Setup(outline, outlineMesh, Parent.outlineMaterial != null ? Parent.outlineMaterial : Parent.bodyMaterial,
                Parent.sortingOrder - 2);
            outline.enabled = outlineOn;
        }

        public void SetOutline(bool on)
        {
            outlineOn = on;
            if (outline != null) outline.enabled = on;
        }

        /// <summary>Collision shape: segment from the body's outer edge to the rod end, radius half a thickness.</summary>
        public void GetSegment(out Vector2 start, out Vector2 end, out float pad)
        {
            if (Parent == null || rod.segments.Count == 0)
            {
                start = end = transform.position;
                pad = 0f;
                return;
            }
            var seg = rod.segments.Count > 0 ? rod.segments[0] : default;
            Vector2 dir = (seg.b - seg.a).normalized;
            start = transform.TransformPoint(seg.a + dir * Parent.thickness * 0.5f);
            end = transform.TransformPoint(seg.b);
            pad = Parent.thickness * 0.5f;
        }

        MeshRenderer GetOrCreatePart(string partName, ref MeshRenderer cached)
        {
            if (cached != null) return cached;
            var t = transform.Find(partName);
            if (t == null)
            {
                t = new GameObject(partName).transform;
                t.SetParent(transform, false);
            }
            if (!t.TryGetComponent<MeshFilter>(out _)) t.gameObject.AddComponent<MeshFilter>();
            if (!t.TryGetComponent<MeshRenderer>(out var mr)) mr = t.gameObject.AddComponent<MeshRenderer>();
            return mr;
        }

        static void Setup(MeshRenderer mr, Mesh m, Material mat, int order)
        {
            mr.GetComponent<MeshFilter>().sharedMesh = m;
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
        }
    }
}
