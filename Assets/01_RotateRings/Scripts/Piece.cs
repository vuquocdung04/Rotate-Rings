using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    public enum MotionKind { None, Rotate, Slide }

    /// <summary>
    /// A thick path (ring, pipe...) that moves along itself through the grips holding it.
    /// - Motion: rings rotate around their center; pipes slide along the straight segment their grips sit on.
    ///   A pipe can't push a bend through a grip.
    /// - Grips: link sleeves of other pieces, or world-fixed pipe clamps. On release, any grip no longer on the
    ///   path (slid past an end, e.g. into a ring's gap) lets go.
    /// - Links / Arms: owned by this piece (child objects, move with it). While a link still holds a child,
    ///   this piece is blocked and only wobbles when pushed. Arms just collide.
    /// - Bodies, links, arms and clamps all collide.
    /// A piece with no grips and no held children breaks.
    /// </summary>
    [ExecuteAlways]
    public class Piece : MonoBehaviour
    {
        public static readonly List<Piece> All = new();

        [Header("Shape")]
        [Tooltip("Centerline in local space. Usually written by a shape component (RingShape, PipeShape).")]
        public PathShape shape = new();
        public float thickness = 0.34f;
        [Tooltip("RingFinal.png row: 0 = flat face with rounded rim (default look), 1 = round tube.")]
        [Range(0, 1)] public int styleRow = 0;
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
        [Tooltip("Drag needed on a blocked piece before it wobbles (degrees for rings, units for pipes).")]
        public float wobbleTrigger = 1.5f;
        public float wobbleTriggerSlide = 0.06f;
        [Tooltip("Peak wobble: degrees for rings, units for pipes.")]
        public float wobbleAmplitude = 6f;
        public float wobbleAmplitudeSlide = 0.1f;
        [Tooltip("Wobble oscillations per second.")]
        public float wobbleFrequency = 5f;
        public float wobbleDuration = 0.45f;
        [Tooltip("Max movement per collision check (degrees / units). Keeps fast drags from tunneling.")]
        public float collisionStep = 2f;
        public float collisionStepSlide = 0.04f;
        [Tooltip("Star burst spawned where something hits (tex_star_005).")]
        public Material sparkMaterial;
        public float sparkCooldown = 0.25f;
        public Material breakMaterial;

        [Header("Audio")]
        [Tooltip("Movement per ratchet click (degrees / units). Clips/volume/pitch live in AudioCatalogSO (RingRatchet).")]
        public float ratchetStep = 12f;
        public float ratchetStepSlide = 0.3f;

        public readonly List<Grip> Grips = new();
        public readonly List<PieceLink> Links = new();
        public readonly List<PieceArm> Arms = new();

        public bool IsBroken { get; private set; }
        public float Angle => transform.eulerAngles.z;
        public Vector2 Center => transform.position;
        public MotionKind Motion { get; private set; }
        /// <summary>World direction of a slide (valid when Motion == Slide).</summary>
        public Vector2 SlideAxis { get; private set; }

        public bool HoldsChild
        {
            get
            {
                foreach (var l in Links)
                    if (l != null && l.Child != null) return true;
                return false;
            }
        }

        const float ProbeSpacing = 0.08f;
        // How far a grip may drift off the path while sliding before it counts as hitting a bend.
        const float BendTolerance = 0.02f;

        MeshRenderer bodyRenderer, shadowRenderer, outlineRenderer;
        Transform shadowTf;
        Mesh mesh, outlineMesh;
        readonly List<Vector2> bodyProbes = new();   // local space, along the centerline
        bool selected;
        bool dirty = true;
        float blockedDrag;
        bool touching;
        float ratchetAccum;
        float lastSparkTime = -10f;
        float wobbleTime = -1f, wobbleDir;
        Vector3 wobbleBasePos;
        float wobbleBaseAngle;

        bool IsWobbling => wobbleTime >= 0f;

        void OnEnable()
        {
            All.Add(this);
            EnsureRenderers();
            dirty = true;
        }

        void OnDisable() => All.Remove(this);

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

        public void SetShape(PathShape newShape)
        {
            shape = newShape;
            dirty = true;
        }

        void Update()
        {
            if (dirty) Rebuild();
            if (Application.isPlaying && IsWobbling) UpdateWobble();
        }

        void LateUpdate()
        {
            // Shadow offset stays in world space regardless of the piece's rotation.
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
                mesh = new Mesh { name = name + "_Path", hideFlags = HideFlags.DontSave };
            if (outlineMesh == null)
                outlineMesh = new Mesh { name = name + "_Outline", hideFlags = HideFlags.DontSave };

            PathMesh.Build(mesh, shape, thickness, styleRow, ToVertexColor(color));
            // Outline: same path, thicker by 2*outlineWidth; the caps grow by outlineWidth automatically.
            PathMesh.Build(outlineMesh, shape, thickness + 2f * outlineWidth, styleRow, ToVertexColor(outlineColor));

            bodyRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            shadowRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            outlineRenderer.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            bodyRenderer.sharedMaterial = bodyMaterial;
            shadowRenderer.sharedMaterial = shadowMaterial;
            outlineRenderer.sharedMaterial = outlineMaterial != null ? outlineMaterial : bodyMaterial;
            bodyRenderer.sortingOrder = sortingOrder;
            outlineRenderer.sortingOrder = sortingOrder - 1;
            outlineRenderer.enabled = selected;
            shadowRenderer.sortingOrder = -1000; // all shadows under all pieces

            // Collision probes: circles of radius thickness/2 along the centerline. The end probes
            // already reach the cap tips (half a thickness past the path ends).
            if (shape != null) shape.Sample(bodyProbes, null, null, 360f, ProbeSpacing);
            else bodyProbes.Clear();

            foreach (var l in GetComponentsInChildren<PieceLink>(true)) l.Layout();
            foreach (var a in GetComponentsInChildren<PieceArm>(true)) a.Layout();
        }

        // Mesh vertex colors are not converted by Unity, so convert sRGB -> linear ourselves.
        public static Color ToVertexColor(Color c) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        // ---------- Path queries (world space) ----------

        public float DistanceToBody(Vector2 world) => shape.Distance(transform.InverseTransformPoint(world));

        public Vector2 ClosestOnBody(Vector2 world)
        {
            Vector2 local = shape.ClosestPoint(transform.InverseTransformPoint(world), out _);
            return transform.TransformPoint(local);
        }

        /// <summary>Picking: distance to the body, plus whether the point is inside a ring's circle.</summary>
        public bool TryPick(Vector2 world, float bodyPickScale, out bool onBody, out float interiorScore)
        {
            onBody = DistanceToBody(world) <= thickness * bodyPickScale;
            interiorScore = float.MaxValue;
            if (shape.TryGetRingCircle(out Vector2 c, out float r))
            {
                float d = Vector2.Distance(transform.InverseTransformPoint(world), c);
                if (d <= r + thickness) interiorScore = d / r;
            }
            return onBody || interiorScore < float.MaxValue;
        }

        // ---------- Motion ----------

        /// <summary>
        /// Rings rotate. Anything else slides along the straight segment its grips sit on;
        /// grips on a bend, or on segments pointing different ways, leave it unable to move.
        /// </summary>
        void ResolveMotion()
        {
            if (shape.TryGetRingCircle(out _, out _))
            {
                Motion = MotionKind.Rotate;
                return;
            }

            Motion = MotionKind.None;
            Vector2 axis = Vector2.zero;
            foreach (var g in Grips)
            {
                if (g == null) continue;
                int i = shape.ClosestSegmentIndex(transform.InverseTransformPoint(g.GripWorldPos), out _);
                var seg = shape.segments[i];
                if (seg.type != SegmentType.Line) return;
                Vector2 dir = ((Vector2)transform.TransformDirection(seg.b - seg.a)).normalized;
                if (axis == Vector2.zero) axis = dir;
                else if (Mathf.Abs(axis.x * dir.y - axis.y * dir.x) > 0.01f) return;
            }
            if (axis == Vector2.zero) return;
            Motion = MotionKind.Slide;
            SlideAxis = axis;
        }

        float StepSize => Motion == MotionKind.Slide ? collisionStepSlide : collisionStep;

        void ApplyMove(float amount)
        {
            if (Motion == MotionKind.Rotate) SetAngle(Angle + amount);
            else if (Motion == MotionKind.Slide) transform.position += (Vector3)(SlideAxis * amount);
        }

        void SetAngle(float z) => transform.rotation = Quaternion.Euler(0f, 0f, z);

        // ---------- Drag ----------

        /// <summary>Outline shows only for pieces the player can actually move (blocked pieces just wobble).</summary>
        void SetSelected(bool on)
        {
            selected = on;
            if (outlineRenderer != null) outlineRenderer.enabled = on;
            var mat = outlineMaterial != null ? outlineMaterial : bodyMaterial;
            foreach (var l in Links)
                if (l != null) l.SetOutline(on, mat, outlineColor, outlineWidth);
            foreach (var a in Arms)
                if (a != null) a.SetOutline(on);
        }

        public void BeginDrag()
        {
            blockedDrag = 0f;
            touching = false;
            ratchetAccum = 0f;
            ResolveMotion();
            SetSelected(!HoldsChild && Motion != MotionKind.None);
        }

        /// <param name="delta">Degrees for rotating pieces, world units along SlideAxis for sliding ones.</param>
        public void Drag(float delta)
        {
            if (IsBroken || delta == 0f || Motion == MotionKind.None) return;

            if (HoldsChild)
            {
                // Blocked: a push in either direction plays one wobble, no real movement.
                if (IsWobbling) return;
                blockedDrag += delta;
                float trigger = Motion == MotionKind.Slide ? wobbleTriggerSlide : wobbleTrigger;
                if (Mathf.Abs(blockedDrag) >= trigger)
                {
                    StartWobble(Mathf.Sign(blockedDrag));
                    blockedDrag = 0f;
                }
                return;
            }

            if (IsWobbling) StopWobble();
            TickRatchet(MoveWithCollision(delta));
        }

        /// <summary>Grips are only resolved on release: any grip no longer on the path lets go.</summary>
        public void EndDrag()
        {
            SetSelected(false);
            if (HoldsChild) return;
            CheckGrips();
            TryBreak();
        }

        /// <summary>Moves in small steps, stopping at the last free pose. Returns how far it actually moved.</summary>
        float MoveWithCollision(float delta)
        {
            // If already overlapping (bad level layout), let it move so it isn't stuck forever.
            bool ignoreCollision = Overlaps(out _);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(delta) / StepSize));
            float step = delta / steps;
            float moved = 0f;
            for (int i = 0; i < steps; i++)
            {
                Vector3 prevPos = transform.position;
                Quaternion prevRot = transform.rotation;
                ApplyMove(step);

                Vector2 hit;
                bool blocked = !GripsAllowPose(out hit) || (!ignoreCollision && Overlaps(out hit));
                if (blocked)
                {
                    transform.SetPositionAndRotation(prevPos, prevRot);
                    OnContact(hit);
                    return moved;
                }
                moved += Mathf.Abs(step);
            }
            touching = false;
            return moved;
        }

        /// <summary>
        /// Sliding: every grip must stay on a straight part of the path, or have slid past a free end.
        /// Running a bend into a grip is blocked. (Rotating rings always stay on their circle.)
        /// </summary>
        bool GripsAllowPose(out Vector2 hit)
        {
            hit = default;
            if (Motion != MotionKind.Slide) return true;
            foreach (var g in Grips)
            {
                if (g == null) continue;
                Vector2 local = transform.InverseTransformPoint(g.GripWorldPos);
                Vector2 q = shape.ClosestPoint(local, out float d);
                if (d <= BendTolerance || shape.IsEndPoint(q)) continue;
                hit = g.GripWorldPos;
                return false;
            }
            return true;
        }

        /// <summary>One click per ratchet step actually moved (blocked drags and wobbles stay silent).</summary>
        void TickRatchet(float moved)
        {
            float step = Motion == MotionKind.Slide ? ratchetStepSlide : ratchetStep;
            if (step <= 0f) return;
            ratchetAccum += moved;
            if (ratchetAccum < step) return;
            ratchetAccum %= step; // at most one click per frame
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

        // ---------- Wobble ----------

        void StartWobble(float dir)
        {
            wobbleBasePos = transform.position;
            wobbleBaseAngle = Angle;
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
            float s = wobbleDir * Mathf.Sin(wobbleTime * wobbleFrequency * Mathf.PI * 2f) * (1f - t) * (1f - t);
            if (Motion == MotionKind.Slide)
                transform.position = wobbleBasePos + (Vector3)(SlideAxis * (wobbleAmplitudeSlide * s));
            else
                SetAngle(wobbleBaseAngle + wobbleAmplitude * s);
        }

        void StopWobble()
        {
            transform.position = wobbleBasePos;
            SetAngle(wobbleBaseAngle);
            wobbleTime = -1f;
        }

        // ---------- Collision ----------

        /// <summary>
        /// Does this piece (body, links, arms) overlap anything else (bodies, links, arms, clamps)?
        /// Everything is probed as circles: along the body centerline, along stems and arms, and one per sleeve.
        /// Pairs that are meant to overlap are skipped: a link vs the child it grips, this body vs grips holding it.
        /// </summary>
        bool Overlaps(out Vector2 hit)
        {
            float bodyPad = thickness * 0.5f;
            foreach (var lp in bodyProbes)
                if (ProbeHits(transform.TransformPoint(lp), bodyPad, null, out hit)) return true;

            foreach (var l in Links)
            {
                if (l == null) continue;
                l.GetCapsule(out Vector2 a, out Vector2 b, out float stemPad, out float sleevePad);
                if (SegmentHits(a, b, stemPad, l, out hit) || ProbeHits(b, sleevePad, l, out hit)) return true;
            }

            foreach (var arm in Arms)
            {
                if (arm == null) continue;
                arm.GetSegment(out Vector2 a, out Vector2 b, out float pad);
                if (SegmentHits(a, b, pad, null, out hit)) return true;
            }

            hit = default;
            return false;
        }

        bool SegmentHits(Vector2 a, Vector2 b, float pad, PieceLink fromLink, out Vector2 hit)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.05f));
            for (int i = 0; i <= n; i++)
                if (ProbeHits(Vector2.Lerp(a, b, (float)i / n), pad, fromLink, out hit)) return true;
            hit = default;
            return false;
        }

        /// <param name="fromLink">The link the probe belongs to, or null for body/arm probes.</param>
        bool ProbeHits(Vector2 p, float pad, PieceLink fromLink, out Vector2 hit)
        {
            foreach (var o in All)
            {
                if (o == null || o == this || o.IsBroken) continue;

                bool skipBody = fromLink != null && o == fromLink.Child;
                if (!skipBody && o.DistanceToBody(p) < pad + o.thickness * 0.5f)
                {
                    hit = (p + o.ClosestOnBody(p)) * 0.5f;
                    return true;
                }

                foreach (var ol in o.Links)
                {
                    if (ol == null) continue;
                    if (fromLink == null && ol.Child == this) continue; // grips me by design
                    ol.GetCapsule(out Vector2 oa, out Vector2 ob, out float oStem, out float oSleeve);
                    if (TouchesSegment(p, pad, oa, ob, oStem, out hit)) return true;
                    if (Vector2.Distance(p, ob) < pad + oSleeve)
                    {
                        hit = (p + ob) * 0.5f;
                        return true;
                    }
                }

                foreach (var oarm in o.Arms)
                {
                    if (oarm == null) continue;
                    oarm.GetSegment(out Vector2 oa, out Vector2 ob, out float oPad);
                    if (TouchesSegment(p, pad, oa, ob, oPad, out hit)) return true;
                }
            }

            foreach (var clamp in PipeClamp.All)
            {
                if (clamp == null || clamp.Child == null || clamp.Child == this) continue;
                Vector2 c = clamp.GripWorldPos;
                if (Vector2.Distance(p, c) < pad + clamp.CollisionRadius)
                {
                    hit = (p + c) * 0.5f;
                    return true;
                }
            }

            hit = default;
            return false;
        }

        static bool TouchesSegment(Vector2 p, float pad, Vector2 a, Vector2 b, float segPad, out Vector2 hit)
        {
            Vector2 q = ClosestOnSegment(p, a, b);
            hit = (p + q) * 0.5f;
            return Vector2.Distance(p, q) < pad + segPad;
        }

        static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        // ---------- Grips ----------

        /// <summary>A grip still holds while it sits on the path; past an end (a gap) it has slipped off.</summary>
        public bool IsGripOnPath(Vector2 world) => shape.closed || DistanceToBody(world) <= thickness * 0.5f;

        void CheckGrips()
        {
            for (int i = Grips.Count - 1; i >= 0; i--)
            {
                var g = Grips[i];
                if (g != null && !IsGripOnPath(g.GripWorldPos)) g.Release();
            }
        }

        public void TryBreak()
        {
            if (IsBroken || !Application.isPlaying) return;
            if (Grips.Count > 0 || HoldsChild) return;
            IsBroken = true;
            RingBreakFX.Spawn(this);
            PlaySfx(AudioKeys.RING_BREAK);
            Destroy(gameObject);
        }
    }
}
