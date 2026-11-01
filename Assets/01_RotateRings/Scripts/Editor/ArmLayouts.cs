using System.Collections.Generic;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// One-click arm sets for the selected piece. Ring layouts are measured from the gap direction,
    /// so they follow the ring when it's rotated. Pipe layouts are derived from the path: corners and leg middles.
    /// </summary>
    public static class ArmLayouts
    {
        public struct ArmSpec
        {
            public float angle, t, side;
        }

        public class Layout
        {
            public string label;
            public List<ArmSpec> arms;
        }

        public static List<Layout> For(Piece p)
        {
            if (p.TryGetComponent<RingShape>(out var ring)) return ForRing(ring.gapCenter, ring.gapAngle <= 0f);
            return ForPipe(p.shape);
        }

        // ---------- Rings ----------

        static List<Layout> ForRing(float gc, bool closed)
        {
            if (closed)
            {
                return new List<Layout>
                {
                    Ring("4 · cross", gc, gc + 90f, gc + 180f, gc + 270f),
                    Ring("3 · even", gc, gc + 120f, gc + 240f),
                    Ring("2 · vertical", gc, gc + 180f),
                    Ring("2 · horizontal", gc + 90f, gc + 270f),
                    Ring("1", gc + 180f),
                };
            }
            // The gap takes the "top" slot: sides are ±90° from it, the even split is 120/240.
            return new List<Layout>
            {
                Ring("3 · sides + opposite", gc + 90f, gc + 270f, gc + 180f),
                Ring("2 · 120 / 240", gc + 120f, gc + 240f),
                Ring("2 · sides", gc + 90f, gc + 270f),
                Ring("1 · opposite", gc + 180f),
                Ring("1 · 120", gc + 120f),
                Ring("1 · 240", gc + 240f),
            };
        }

        static Layout Ring(string label, params float[] angles)
        {
            var arms = new List<ArmSpec>();
            foreach (float a in angles) arms.Add(new ArmSpec { angle = Mathf.Repeat(a, 360f) });
            return new Layout { label = label, arms = arms };
        }

        // ---------- Pipes ----------

        static readonly List<Vector2> Samples = new();

        static List<Layout> ForPipe(PathShape shape)
        {
            var layouts = new List<Layout>();
            if (shape == null || shape.segments.Count == 0) return layouts;

            float length = Mathf.Max(1e-4f, shape.Length);
            shape.Sample(Samples, null, null, 10f, 0.1f);
            Vector2 centroid = Vector2.zero;
            foreach (var s in Samples) centroid += s;
            centroid /= Mathf.Max(1, Samples.Count);

            var cornersOut = new List<ArmSpec>();
            var cornersIn = new List<ArmSpec>();
            var legsOut = new List<ArmSpec>();
            var legsIn = new List<ArmSpec>();
            float sBase = 0f;
            foreach (var seg in shape.segments)
            {
                float len = seg.Length;
                float t = (sBase + len * 0.5f) / length;
                if (seg.type == SegmentType.Arc)
                {
                    // A left turn has its center on the left, so the outside of the bend is on the right (+1).
                    float outSide = seg.sweepDeg > 0f ? 1f : -1f;
                    cornersOut.Add(new ArmSpec { t = t, side = outSide });
                    cornersIn.Add(new ArmSpec { t = t, side = -outSide });
                }
                else
                {
                    Vector2 mid = seg.Point(0.5f);
                    Vector2 tan = seg.Tangent(0.5f);
                    Vector2 right = new(tan.y, -tan.x);
                    float outSide = Vector2.Dot(centroid - mid, right) > 0f ? -1f : 1f;
                    legsOut.Add(new ArmSpec { t = t, side = outSide });
                    legsIn.Add(new ArmSpec { t = t, side = -outSide });
                }
                sBase += len;
            }

            if (cornersOut.Count == 0)
            {
                // Straight pipe: no inside/outside, just the two sides.
                var both = new List<ArmSpec>(legsOut);
                both.AddRange(legsIn);
                layouts.Add(new Layout { label = "Middle · side A", arms = legsOut });
                layouts.Add(new Layout { label = "Middle · side B", arms = legsIn });
                layouts.Add(new Layout { label = "Middle · both", arms = both });
                return layouts;
            }

            layouts.Add(new Layout { label = $"{cornersOut.Count} · corners out", arms = cornersOut });
            layouts.Add(new Layout { label = $"{cornersIn.Count} · corners in", arms = cornersIn });
            layouts.Add(new Layout { label = $"{legsOut.Count} · legs out", arms = legsOut });
            layouts.Add(new Layout { label = $"{legsIn.Count} · legs in", arms = legsIn });
            return layouts;
        }
    }
}
