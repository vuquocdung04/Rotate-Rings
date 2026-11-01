using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// A link owned by a parent piece (must be a child object of it, so it moves with the parent).
    /// It sits at the closest pair of points between the parent's and the child's paths: a stem runs from the
    /// parent's centerline to the child's centerline, where a sleeve grips the child's body.
    /// For two rings that pair lies on the line between their centers.
    /// </summary>
    [ExecuteAlways]
    public class PieceLink : Grip
    {
        public float stemWidth = 0.3f;
        public Vector2 sleeveSize = new(0.6f, 0.45f);
        [Tooltip("Visual nudge of the sleeve (and stem end). x: along the gripped piece, y: along the stem, away from the parent. " +
                 "Gameplay still grips the exact point on the child's centerline.")]
        public Vector2 sleeveOffset = new(0f, 0.03f);
        public SpriteRenderer stem;
        public SpriteRenderer sleeve;

        public Piece Parent { get; private set; }
        public override Vector2 GripWorldPos => transform.TransformPoint(gripLocal);

        Vector2 anchorLocal; // stem root on the parent's centerline, parent local space
        Vector2 gripLocal;   // grip point on the child's centerline, this transform's local space

        void OnEnable()
        {
            Parent = GetComponentInParent<Piece>();
            if (Parent != null && !Parent.Links.Contains(this)) Parent.Links.Add(this);
            RegisterWithChild();
            Layout();
        }

        void OnDisable()
        {
            if (Parent != null) Parent.Links.Remove(this);
            UnregisterFromChild();
        }

        void Update()
        {
            if (!Application.isPlaying) Layout();
        }

        // The parent may now be free too.
        protected override void OnReleased()
        {
            if (Parent != null) Parent.TryBreak();
        }

        /// <summary>Places stem and sleeve. Only meaningful while attached (needs the child piece).</summary>
        public void Layout()
        {
            if (Parent == null) Parent = GetComponentInParent<Piece>();
            if (Parent == null || child == null || !attached || stem == null || sleeve == null) return;
            if (Parent.shape == null || child.shape == null || Parent.shape.segments.Count == 0 || child.shape.segments.Count == 0) return;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            ClosestPoints(Parent, child, out Vector2 anchor, out Vector2 gripPos);
            anchorLocal = Parent.transform.InverseTransformPoint(anchor);
            gripLocal = transform.InverseTransformPoint(gripPos);
            Vector2 dir = gripPos - anchor;
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.up;
            Vector2 side = new(-dir.y, dir.x); // along the child's path at the grip
            float dirDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float z = transform.position.z;

            // Sleeve sits square to the stem, which is also square to the child's path at that point.
            Vector2 sleevePos = gripPos + side * sleeveOffset.x + dir * sleeveOffset.y;
            var sleeveTf = sleeve.transform;
            sleeveTf.position = new Vector3(sleevePos.x, sleevePos.y, z);
            sleeveTf.rotation = Quaternion.Euler(0f, 0f, dirDeg + 90f);
            SetWorldSize(sleeve, sleeveSize);

            // Stem from the parent's centerline to just under the sleeve, extended half a thickness
            // each way so it tucks under the parent body. It follows the sleeve's sideways offset.
            Vector2 stemRoot = anchor + side * sleeveOffset.x;
            float len = Mathf.Max(0.01f, Vector2.Dot(sleevePos - stemRoot, dir) - sleeveSize.y * 0.4f);
            Vector2 mid = stemRoot + dir * (len * 0.5f);
            var stemTf = stem.transform;
            stemTf.position = new Vector3(mid.x, mid.y, z);
            stemTf.rotation = Quaternion.Euler(0f, 0f, dirDeg);
            SetWorldSize(stem, new Vector2(len + Parent.thickness, stemWidth));

            Tint(stem, Parent.color);
            Tint(sleeve, Parent.color);
            // Stem runs under both pieces; only the sleeve is drawn on top.
            stem.sortingOrder = Mathf.Min(Parent.sortingOrder, child.sortingOrder) - 1;
            sleeve.sortingOrder = Mathf.Max(Parent.sortingOrder, child.sortingOrder) + 2;
        }

        static readonly List<Vector2> Samples = new();

        /// <summary>Closest pair of points (world) between two pieces' centerlines: sample a, project onto b.</summary>
        static void ClosestPoints(Piece a, Piece b, out Vector2 onA, out Vector2 onB)
        {
            a.shape.Sample(Samples, null, null, 1f, 0.02f);
            onA = onB = a.transform.position;
            float best = float.MaxValue;
            foreach (var local in Samples)
            {
                Vector2 pa = a.transform.TransformPoint(local);
                Vector2 pb = b.ClosestOnBody(pa);
                float d = (pb - pa).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    onA = pa;
                    onB = pb;
                }
            }
        }

        /// <summary>Collision shape (what you see): stem segment from the parent's outer edge to the sleeve, plus the sleeve as a circle.</summary>
        public void GetCapsule(out Vector2 start, out Vector2 end, out float stemPad, out float sleevePad)
        {
            Vector2 anchor = Parent.transform.TransformPoint(anchorLocal);
            end = sleeve != null ? (Vector2)sleeve.transform.position : GripWorldPos;
            Vector2 dir = (end - anchor).normalized;
            start = anchor + dir * (Parent.thickness * 0.5f);
            stemPad = stemWidth * 0.5f;
            sleevePad = Mathf.Max(sleeveSize.x, sleeveSize.y) * 0.5f;
        }

        // ---------- Selection outline ----------

        SpriteRenderer stemOutline, sleeveOutline;

        public void SetOutline(bool on, Material mat, Color color, float width)
        {
            if (on)
            {
                if (stem != null) ShowOutline(stem, ref stemOutline, mat, color, width);
                if (sleeve != null) ShowOutline(sleeve, ref sleeveOutline, mat, color, width);
            }
            else
            {
                if (stemOutline != null) stemOutline.enabled = false;
                if (sleeveOutline != null) sleeveOutline.enabled = false;
            }
        }

        /// <summary>A copy of the sprite one step behind, grown by width on each side and tinted flat.</summary>
        static void ShowOutline(SpriteRenderer src, ref SpriteRenderer outline, Material mat, Color color, float width)
        {
            if (outline == null)
            {
                var go = new GameObject("Outline") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(src.transform, false);
                outline = go.AddComponent<SpriteRenderer>();
            }
            outline.sprite = src.sprite;
            outline.sharedMaterial = mat;
            outline.sortingOrder = src.sortingOrder - 1;
            Tint(outline, color);

            if (src.drawMode != SpriteDrawMode.Simple)
            {
                // 9-sliced: grow the size so the corners keep their radius.
                outline.drawMode = src.drawMode;
                outline.size = src.size + Vector2.one * (2f * width);
                outline.transform.localScale = Vector3.one;
            }
            else
            {
                Vector2 size = Vector2.Scale(src.sprite.bounds.size, src.transform.lossyScale);
                size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
                outline.transform.localScale = new Vector3((size.x + 2f * width) / size.x, (size.y + 2f * width) / size.y, 1f);
            }
            outline.enabled = true;
        }

        static MaterialPropertyBlock mpb;

        // Unity 6 sprites don't pass SpriteRenderer.color as vertex color to custom shaders; use _Color instead.
        static void Tint(SpriteRenderer sr, Color c)
        {
            mpb ??= new MaterialPropertyBlock();
            sr.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            sr.SetPropertyBlock(mpb);
        }

        static void SetWorldSize(SpriteRenderer sr, Vector2 size)
        {
            if (sr.sprite == null) return;
            if (sr.drawMode != SpriteDrawMode.Simple)
            {
                // 9-sliced sprites (LinkBlock) resize without stretching their rounded corners.
                sr.transform.localScale = Vector3.one;
                sr.size = size;
                return;
            }
            Vector2 s = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
        }
    }
}
