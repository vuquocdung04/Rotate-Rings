using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// A link owned by a parent ring (must be a child object of it, so it rotates with the parent).
    /// A stem goes out radially from the parent at <see cref="angle"/> until it meets the child ring's
    /// centerline, where a sleeve grips the child's body.
    /// </summary>
    [ExecuteAlways]
    public class RingLink : MonoBehaviour
    {
        public Ring child;
        [Tooltip("Point the link along the line between both ring centers, so the child passes straight through the sleeve. Overrides angle.")]
        public bool aimAtChild = true;
        [Tooltip("Local angle on the parent ring, degrees. Used when aimAtChild is off.")]
        public float angle = -90f;
        public float stemWidth = 0.3f;
        public Vector2 sleeveSize = new(0.55f, 0.5f);
        public SpriteRenderer stem;
        public SpriteRenderer sleeve;

        public Ring Parent { get; private set; }
        public Ring Child => attached ? child : null;
        public Vector2 SleeveWorldPos => sleeve != null ? (Vector2)sleeve.transform.position : (Vector2)transform.position;

        bool attached = true;

        void OnEnable()
        {
            Parent = GetComponentInParent<Ring>();
            if (Parent != null && !Parent.Links.Contains(this)) Parent.Links.Add(this);
            if (Application.isPlaying && attached && child != null && !child.Locks.Contains(this))
                child.Locks.Add(this);
            Layout();
        }

        void OnDisable()
        {
            if (Parent != null) Parent.Links.Remove(this);
            if (child != null) child.Locks.Remove(this);
        }

        void Update()
        {
            if (!Application.isPlaying) Layout();
        }

        public void Release()
        {
            if (!attached) return;
            attached = false;
            var c = child;
            if (c != null)
            {
                c.Locks.Remove(this);
                c.TryBreak();
            }
            if (Parent != null) Parent.TryBreak();
        }

        /// <summary>Places stem and sleeve. Only meaningful while attached (needs the child ring).</summary>
        public void Layout()
        {
            if (Parent == null) Parent = GetComponentInParent<Ring>();
            if (Parent == null || child == null || !attached || stem == null || sleeve == null) return;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            Vector2 p = Parent.Center;
            Vector2 c = child.Center;
            if (aimAtChild)
                angle = Mathf.Atan2(c.y - p.y, c.x - p.x) * Mathf.Rad2Deg - Parent.Angle;
            float worldAngle = (angle + Parent.Angle) * Mathf.Deg2Rad;
            Vector2 dir = new(Mathf.Cos(worldAngle), Mathf.Sin(worldAngle));

            // Ray from parent center along dir vs child centerline circle: |p + dir*t - c| = R
            Vector2 pc = p - c;
            float b = Vector2.Dot(dir, pc);
            float disc = b * b - (pc.sqrMagnitude - child.radius * child.radius);
            float t;
            if (disc >= 0f)
            {
                float sq = Mathf.Sqrt(disc);
                float t0 = -b - sq, t1 = -b + sq;
                t = t0 >= Parent.radius ? t0 : t1;
            }
            else
            {
                t = -b; // miss: closest approach
                Debug.LogWarning($"[RingLink] {name}: ray from {Parent.name} misses {child.name}.", this);
            }

            Vector2 sleevePos = p + dir * t;

            // Sleeve sits square to the stem (not tilted along the child's curve).
            var sleeveTf = sleeve.transform;
            sleeveTf.position = new Vector3(sleevePos.x, sleevePos.y, transform.position.z);
            sleeveTf.rotation = Quaternion.Euler(0f, 0f, worldAngle * Mathf.Rad2Deg + 90f);
            SetWorldSize(sleeve, sleeveSize);

            float start = Parent.radius;
            float end = t - sleeveSize.y * 0.4f;
            float len = Mathf.Max(0.01f, end - start);
            Vector2 mid = p + dir * (start + len * 0.5f);
            var stemTf = stem.transform;
            stemTf.position = new Vector3(mid.x, mid.y, transform.position.z);
            stemTf.rotation = Quaternion.Euler(0f, 0f, worldAngle * Mathf.Rad2Deg);
            SetWorldSize(stem, new Vector2(len + Parent.thickness, stemWidth));

            Tint(stem, Parent.color);
            Tint(sleeve, Parent.color);
            // Stem runs under both rings; only the sleeve is drawn on top.
            stem.sortingOrder = Mathf.Min(Parent.sortingOrder, child.sortingOrder) - 1;
            sleeve.sortingOrder = Mathf.Max(Parent.sortingOrder, child.sortingOrder) + 2;
        }

        /// <summary>Collision shape: stem segment from the parent's outer edge to the sleeve, plus the sleeve as a circle.</summary>
        public void GetCapsule(out Vector2 start, out Vector2 end, out float stemPad, out float sleevePad)
        {
            Vector2 c = Parent.Center;
            end = SleeveWorldPos;
            Vector2 dir = (end - c).normalized;
            start = c + dir * (Parent.radius + Parent.thickness * 0.5f);
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

            Vector2 size = Vector2.Scale(src.sprite.bounds.size, src.transform.lossyScale);
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            outline.transform.localScale = new Vector3((size.x + 2f * width) / size.x, (size.y + 2f * width) / size.y, 1f);
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
            Vector2 s = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
        }
    }
}
