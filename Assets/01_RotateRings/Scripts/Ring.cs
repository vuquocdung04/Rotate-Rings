using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// A C-shaped ring. Rotates around its own center.
    /// - Locks: sleeves of other rings' links gripping this ring's body. A lock is released when the gap passes it.
    /// - Links: owned by this ring (child objects, rotate with it). While a link still holds a child ring,
    ///   this ring is blocked and only wobbles when pushed.
    /// A ring with no locks and no held children breaks.
    /// </summary>
    [ExecuteAlways]
    public class Ring : MonoBehaviour
    {
        public static readonly List<Ring> All = new();

        [Header("Shape")]
        public float radius = 1.25f;
        public float thickness = 0.34f;
        [Range(10f, 180f)] public float gapAngle = 70f;
        [Tooltip("Local angle of the gap center, degrees (90 = up).")]
        public float gapCenter = 90f;
        [Range(8, 256)] public int segments = 96;
        [Range(0, 1)] public int styleRow = 1;
        public Color color = Color.white;

        [Header("Rendering")]
        public Material bodyMaterial;
        public Material shadowMaterial;
        public Vector2 shadowOffset = new(0.02f, -0.07f);
        public int sortingOrder;

        [Header("Selection Outline")]
        [Tooltip("Optional flat material for the outline; falls back to bodyMaterial.")]
        public Material outlineMaterial;
        public Color outlineColor = new(0.15f, 0.95f, 1f);
        public float outlineWidth = 0.06f;

        [Header("Gameplay")]
        [Tooltip("Drag needed on a blocked ring before it wobbles, degrees.")]
        public float wobbleTrigger = 1.5f;
        [Tooltip("Peak wobble angle, degrees.")]
        public float wobbleAmplitude = 6f;
        [Tooltip("Wobble oscillations per second.")]
        public float wobbleFrequency = 5f;
        public float wobbleDuration = 0.45f;
        [Tooltip("Max rotation per collision check, degrees. Keeps fast drags from tunneling through a ring.")]
        public float collisionStep = 2f;
        [Tooltip("Star burst spawned where a link hits something (tex_star_005).")]
        public Material sparkMaterial;
        public float sparkCooldown = 0.25f;

        [Header("Audio")]
        [Tooltip("Degrees of real rotation per ratchet click. Clips/volume/pitch live in AudioCatalogSO (RingRatchet).")]
        public float ratchetStep = 12f;
        public Material breakMaterial;

        public readonly List<RingLink> Locks = new();
        public readonly List<RingLink> Links = new();

        public bool IsBroken { get; private set; }
        public float Angle => transform.eulerAngles.z;
        public Vector2 Center => transform.position;

        public bool HoldsChild
        {
            get
            {
                foreach (var l in Links)
                    if (l != null && l.Child != null) return true;
                return false;
            }
        }

        MeshRenderer bodyRenderer, shadowRenderer, outlineRenderer;
        Transform shadowTf;
        Mesh mesh, outlineMesh;
        bool selected;
        bool dirty = true;
        float blockedDrag;
        bool touching;
        float ratchetAccum;
        float lastSparkTime = -10f;
        float wobbleTime = -1f, wobbleBase, wobbleDir;

        bool IsWobbling => wobbleTime >= 0f;

        void OnEnable()
        {
            All.Add(this);
            EnsureRenderers();
            dirty = true;
        }

        void OnDisable()
        {
            All.Remove(this);
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

        void Update()
        {
            if (dirty) Rebuild();

            if (Application.isPlaying && IsWobbling) UpdateWobble();
        }

        void LateUpdate()
        {
            // Shadow offset stays in world space regardless of the ring's rotation.
            if (shadowTf != null)
                shadowTf.position = transform.position + (Vector3)shadowOffset + new Vector3(0f, 0f, 0.01f);
        }

        // ---------- Rendering ----------

        void EnsureRenderers()
        {
            bodyRenderer = GetOrCreatePart("Body");
            shadowRenderer = GetOrCreatePart("Shadow");
            outlineRenderer = GetOrCreatePart("Outline");
            shadowTf = shadowRenderer.transform;
        }

        MeshRenderer GetOrCreatePart(string partName)
        {
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

        public void Rebuild()
        {
            dirty = false;
            if (bodyRenderer == null) EnsureRenderers();
            if (mesh == null)
                mesh = new Mesh { name = name + "_Arc", hideFlags = HideFlags.DontSave };

            ArcRingMesh.Build(mesh, radius, thickness, gapAngle, gapCenter, segments, styleRow, ToVertexColor(color));

            // Outline: same arc, thicker by 2*outlineWidth, gap narrowed so the caps grow by outlineWidth too.
            if (outlineMesh == null)
                outlineMesh = new Mesh { name = name + "_Outline", hideFlags = HideFlags.DontSave };
            float outlineGap = Mathf.Max(1f, gapAngle - 2f * outlineWidth / radius * Mathf.Rad2Deg);
            ArcRingMesh.Build(outlineMesh, radius, thickness + 2f * outlineWidth, outlineGap, gapCenter, segments,
                styleRow, ToVertexColor(outlineColor));

            bodyRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadowRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            outlineRenderer.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            bodyRenderer.sharedMaterial = bodyMaterial;
            shadowRenderer.sharedMaterial = shadowMaterial;
            outlineRenderer.sharedMaterial = outlineMaterial != null ? outlineMaterial : bodyMaterial;
            bodyRenderer.sortingOrder = sortingOrder;
            outlineRenderer.sortingOrder = sortingOrder - 1;
            outlineRenderer.enabled = selected;
            shadowRenderer.sortingOrder = -1000; // all shadows under all rings

            foreach (var l in GetComponentsInChildren<RingLink>(true)) l.Layout();
        }

        // ---------- Rotation ----------

        // Mesh vertex colors are not converted by Unity, so convert sRGB -> linear ourselves.
        static Color ToVertexColor(Color c) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        /// <summary>Outline shows only for rings the player can actually move (blocked rings just wobble).</summary>
        void SetSelected(bool on)
        {
            selected = on;
            if (outlineRenderer != null) outlineRenderer.enabled = on;
            var mat = outlineMaterial != null ? outlineMaterial : bodyMaterial;
            foreach (var l in Links)
                if (l != null) l.SetOutline(on, mat, outlineColor, outlineWidth);
        }

        public void BeginDrag()
        {
            blockedDrag = 0f;
            touching = false;
            ratchetAccum = 0f;
            SetSelected(!HoldsChild);
        }

        public void Drag(float delta)
        {
            if (IsBroken || delta == 0f) return;

            if (HoldsChild)
            {
                // Blocked: a push in either direction plays one wobble, no real rotation.
                if (IsWobbling) return;
                blockedDrag += delta;
                if (Mathf.Abs(blockedDrag) >= wobbleTrigger)
                {
                    StartWobble(Mathf.Sign(blockedDrag));
                    blockedDrag = 0f;
                }
                return;
            }

            if (IsWobbling) StopWobble();

            // Step so fast drags can't skip over a ring; stop at the last free angle.
            // If links already overlap something (bad level layout), let the ring move so it isn't stuck forever.
            float startAngle = Angle;
            RotateWithCollision(delta);
            TickRatchet(Mathf.Abs(Mathf.DeltaAngle(startAngle, Angle)));
        }

        void RotateWithCollision(float delta)
        {
            bool ignoreCollision = LinksCollide(out _);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / collisionStep));
            float step = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                float prev = Angle;
                SetAngle(prev + step);
                if (!ignoreCollision && LinksCollide(out Vector2 hit))
                {
                    SetAngle(prev);
                    OnContact(hit);
                    return;
                }
            }
            touching = false;
        }

        /// <summary>One click per ratchetStep degrees actually rotated (blocked drags and wobbles stay silent).</summary>
        void TickRatchet(float rotated)
        {
            if (ratchetStep <= 0f) return;
            ratchetAccum += rotated;
            if (ratchetAccum < ratchetStep) return;
            ratchetAccum %= ratchetStep; // at most one click per frame
            PlaySfx(AudioKeys.RING_RATCHET);
        }

        static void PlaySfx(string key)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key);
        }

        /// <summary>Sparks only on a fresh contact, not every frame the player keeps pushing into it.</summary>
        void OnContact(Vector2 hit)
        {
            if (!touching && Time.time - lastSparkTime >= sparkCooldown)
            {
                RingSparkFX.Spawn(hit, sparkMaterial);
                PlaySfx(AudioKeys.RING_BLOCKED);
                lastSparkTime = Time.time;
            }
            touching = true;
        }

        /// <summary>Locks are only resolved on release: any sleeve sitting in the gap lets go.</summary>
        public void EndDrag()
        {
            SetSelected(false);
            if (!HoldsChild) CheckLocks();
        }

        void SetAngle(float z) => transform.rotation = Quaternion.Euler(0f, 0f, z);

        void StartWobble(float dir)
        {
            wobbleBase = Angle;
            wobbleDir = dir;
            wobbleTime = 0f;
            PlaySfx(AudioKeys.RING_BLOCKED);
        }

        void UpdateWobble()
        {
            wobbleTime += Time.deltaTime;
            if (wobbleTime >= wobbleDuration)
            {
                StopWobble();
                return;
            }
            // Damped sine, starting toward the push direction.
            float t = wobbleTime / wobbleDuration;
            float s = Mathf.Sin(wobbleTime * wobbleFrequency * Mathf.PI * 2f) * (1f - t) * (1f - t);
            SetAngle(wobbleBase + wobbleDir * wobbleAmplitude * s);
        }

        void StopWobble()
        {
            SetAngle(wobbleBase);
            wobbleTime = -1f;
        }

        // ---------- Collision ----------

        /// <summary>
        /// Do this ring's links (stem + sleeve) overlap another ring's body or another ring's links?
        /// The link is sampled as circles along the stem plus one fat circle for the sleeve.
        /// </summary>
        bool LinksCollide(out Vector2 hit)
        {
            foreach (var l in Links)
            {
                if (l == null) continue;
                l.GetCapsule(out Vector2 a, out Vector2 b, out float stemPad, out float sleevePad);
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.05f));
                for (int i = 0; i <= n + 1; i++)
                {
                    bool isSleeve = i == n + 1;
                    Vector2 p = isSleeve ? b : Vector2.Lerp(a, b, (float)i / n);
                    if (PointHitsOthers(l, p, isSleeve ? sleevePad : stemPad, out hit)) return true;
                }
            }
            hit = default;
            return false;
        }

        bool PointHitsOthers(RingLink self, Vector2 p, float pad, out Vector2 hit)
        {
            foreach (var o in All)
            {
                if (o == null || o == this || o.IsBroken) continue;

                // The child this link grips is meant to overlap it.
                if (o != self.Child && o.DistanceToBody(p) < pad + o.thickness * 0.5f)
                {
                    hit = p;
                    return true;
                }

                foreach (var ol in o.Links)
                {
                    if (ol == null) continue;
                    ol.GetCapsule(out Vector2 oa, out Vector2 ob, out float oStem, out float oSleeve);
                    Vector2 q = ClosestOnSegment(p, oa, ob);
                    if (Vector2.Distance(p, q) < pad + oStem)
                    {
                        hit = (p + q) * 0.5f;
                        return true;
                    }
                    if (Vector2.Distance(p, ob) < pad + oSleeve)
                    {
                        hit = (p + ob) * 0.5f;
                        return true;
                    }
                }
            }
            hit = default;
            return false;
        }

        static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        /// <summary>Distance from a world point to the ring's centerline arc (gap excluded, caps included).</summary>
        public float DistanceToBody(Vector2 p)
        {
            Vector2 d = p - Center;
            float local = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - Angle;
            // Cap centers sit half a thickness (in arc length) past the visible gap edges.
            float capDeg = 0.5f * thickness / radius * Mathf.Rad2Deg;
            float halfOpen = gapAngle * 0.5f + capDeg;
            if (Mathf.Abs(Mathf.DeltaAngle(local, gapCenter)) >= halfOpen)
                return Mathf.Abs(d.magnitude - radius);

            float a1 = (gapCenter + halfOpen + Angle) * Mathf.Deg2Rad;
            float a2 = (gapCenter - halfOpen + Angle) * Mathf.Deg2Rad;
            Vector2 e1 = Center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector2 e2 = Center + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * radius;
            return Mathf.Min(Vector2.Distance(p, e1), Vector2.Distance(p, e2));
        }

        // ---------- Locks ----------

        /// <summary>True if the world point lies in this ring's gap (angularly).</summary>
        public bool IsInGap(Vector2 world)
        {
            Vector2 d = world - Center;
            float local = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - Angle;
            return Mathf.Abs(Mathf.DeltaAngle(local, gapCenter)) < gapAngle * 0.5f;
        }

        void CheckLocks()
        {
            for (int i = Locks.Count - 1; i >= 0; i--)
            {
                var l = Locks[i];
                if (l != null && IsInGap(l.SleeveWorldPos)) l.Release();
            }
        }

        public void TryBreak()
        {
            if (IsBroken || !Application.isPlaying) return;
            if (Locks.Count > 0 || HoldsChild) return;
            IsBroken = true;
            RingBreakFX.Spawn(this);
            PlaySfx(AudioKeys.RING_BREAK);
            Destroy(gameObject);
        }
    }
}
