using System;
using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    public enum SegmentType { Line, Arc }

    /// <summary>One piece of a path, in the piece's local space. Arcs go counter-clockwise when sweep > 0.</summary>
    [Serializable]
    public struct PathSegment
    {
        public SegmentType type;
        public Vector2 a, b;
        public Vector2 center;
        public float radius;
        public float startDeg, sweepDeg;

        public static PathSegment Line(Vector2 a, Vector2 b) => new() { type = SegmentType.Line, a = a, b = b };

        public static PathSegment Arc(Vector2 center, float radius, float startDeg, float sweepDeg) =>
            new() { type = SegmentType.Arc, center = center, radius = radius, startDeg = startDeg, sweepDeg = sweepDeg };

        public float Length => type == SegmentType.Line
            ? Vector2.Distance(a, b)
            : Mathf.Abs(sweepDeg) * Mathf.Deg2Rad * radius;

        public Vector2 Point(float t)
        {
            if (type == SegmentType.Line) return Vector2.Lerp(a, b, t);
            float ang = (startDeg + sweepDeg * t) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
        }

        public Vector2 Tangent(float t)
        {
            if (type == SegmentType.Line) return (b - a).normalized;
            float ang = (startDeg + sweepDeg * t) * Mathf.Deg2Rad;
            return new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * Mathf.Sign(sweepDeg);
        }

        /// <summary>Closest point on this segment to p; t is the normalized position along the segment.</summary>
        public Vector2 Closest(Vector2 p, out float t)
        {
            if (type == SegmentType.Line)
            {
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                t = len2 < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                return Vector2.Lerp(a, b, t);
            }

            float sweep = Mathf.Abs(sweepDeg);
            float ang = Mathf.Atan2(p.y - center.y, p.x - center.x) * Mathf.Rad2Deg;
            float offset = Mathf.Repeat((ang - startDeg) * Mathf.Sign(sweepDeg), 360f);
            if (offset <= sweep) t = sweep < 1e-4f ? 0f : offset / sweep;
            else t = offset - sweep < 360f - offset ? 1f : 0f; // outside the arc: nearer end
            return Point(t);
        }
    }

    /// <summary>
    /// Centerline of a piece: lines and arcs joined end to end, in local space.
    /// Distances along the path ("s") run from 0 to Length.
    /// </summary>
    [Serializable]
    public class PathShape
    {
        public List<PathSegment> segments = new();
        public bool closed;

        public float Length
        {
            get
            {
                float sum = 0f;
                foreach (var seg in segments) sum += seg.Length;
                return sum;
            }
        }

        public Vector2 Start => segments.Count > 0 ? segments[0].Point(0f) : Vector2.zero;
        public Vector2 End => segments.Count > 0 ? segments[^1].Point(1f) : Vector2.zero;

        /// <summary>Single-arc paths (rings) have an interior the player can grab.</summary>
        public bool TryGetRingCircle(out Vector2 center, out float radius)
        {
            if (segments.Count == 1 && segments[0].type == SegmentType.Arc)
            {
                center = segments[0].center;
                radius = segments[0].radius;
                return true;
            }
            center = default;
            radius = 0f;
            return false;
        }

        public Vector2 Evaluate(float s, out Vector2 tangent)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                float len = segments[i].Length;
                if (s <= len || i == segments.Count - 1)
                {
                    float t = len < 1e-6f ? 0f : Mathf.Clamp01(s / len);
                    tangent = segments[i].Tangent(t);
                    return segments[i].Point(t);
                }
                s -= len;
            }
            tangent = Vector2.right;
            return Vector2.zero;
        }

        public Vector2 ClosestPoint(Vector2 p, out float distance)
        {
            Vector2 best = p;
            distance = float.MaxValue;
            foreach (var seg in segments)
            {
                Vector2 q = seg.Closest(p, out _);
                float d = Vector2.Distance(p, q);
                if (d < distance)
                {
                    distance = d;
                    best = q;
                }
            }
            return best;
        }

        public float Distance(Vector2 p)
        {
            ClosestPoint(p, out float d);
            return d;
        }

        /// <summary>Closest point as a distance along the path (s), with the path tangent there.</summary>
        public float ClosestAlong(Vector2 p, out Vector2 tangent)
        {
            float sBase = 0f, bestS = 0f, bestDist = float.MaxValue;
            tangent = Vector2.right;
            foreach (var seg in segments)
            {
                float len = seg.Length;
                Vector2 q = seg.Closest(p, out float t);
                float d = (p - q).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestS = sBase + len * t;
                    tangent = seg.Tangent(t);
                }
                sBase += len;
            }
            return bestS;
        }

        public int ClosestSegmentIndex(Vector2 p, out Vector2 closest)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            closest = p;
            for (int i = 0; i < segments.Count; i++)
            {
                Vector2 q = segments[i].Closest(p, out _);
                float d = (p - q).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                    closest = q;
                }
            }
            return best;
        }

        /// <summary>True if q is one of the path's two free ends (always false for closed paths).</summary>
        public bool IsEndPoint(Vector2 q, float tolerance = 1e-3f) =>
            !closed && segments.Count > 0 &&
            ((q - Start).sqrMagnitude < tolerance * tolerance || (q - End).sqrMagnitude < tolerance * tolerance);

        /// <summary>
        /// Samples the path. Arcs are split at least every maxArcStepDeg; any segment at least every maxStep length.
        /// Shared joints are emitted once; a closed path does not repeat its first point.
        /// </summary>
        public void Sample(List<Vector2> points, List<Vector2> tangents, List<float> distances,
            float maxArcStepDeg, float maxStep = float.MaxValue)
        {
            points.Clear(); tangents?.Clear(); distances?.Clear();
            float sBase = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                float len = seg.Length;
                int n = Mathf.Max(1, Mathf.CeilToInt(len / maxStep));
                if (seg.type == SegmentType.Arc)
                    n = Mathf.Max(n, Mathf.CeilToInt(Mathf.Abs(seg.sweepDeg) / maxArcStepDeg));
                for (int k = i == 0 ? 0 : 1; k <= n; k++)
                {
                    if (closed && i == segments.Count - 1 && k == n) break;
                    float t = (float)k / n;
                    points.Add(seg.Point(t));
                    tangents?.Add(seg.Tangent(t));
                    distances?.Add(sBase + len * t);
                }
                sBase += len;
            }
        }

        // ---------- Presets ----------

        /// <summary>
        /// Open polyline (pipe) with every corner rounded by a fillet arc of cornerRadius.
        /// Keep cornerRadius >= thickness so the mesh strip (2x thickness wide) doesn't fold on the inside.
        /// </summary>
        public static PathShape Polyline(IList<Vector2> points, float cornerRadius)
        {
            var shape = new PathShape();
            if (points == null || points.Count < 2) return shape;

            Vector2 cursor = points[0];
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector2 p = points[i];
                Vector2 d1 = (p - points[i - 1]).normalized;
                Vector2 d2 = (points[i + 1] - p).normalized;
                float turn = Vector2.SignedAngle(d1, d2); // + = left turn (CCW)
                if (Mathf.Abs(turn) < 0.5f) continue;     // collinear: the next line just continues

                float halfTan = Mathf.Tan(Mathf.Abs(turn) * 0.5f * Mathf.Deg2Rad);
                // Fillet tangent length, limited so it fits both neighbouring legs.
                float t = Mathf.Min(cornerRadius * halfTan, Vector2.Distance(cursor, p),
                    Vector2.Distance(p, points[i + 1]) * 0.5f);
                float r = t / halfTan;

                Vector2 a = p - d1 * t;
                if ((a - cursor).sqrMagnitude > 1e-8f) shape.segments.Add(PathSegment.Line(cursor, a));

                Vector2 toCenter = turn > 0f ? new Vector2(-d1.y, d1.x) : new Vector2(d1.y, -d1.x);
                Vector2 c = a + toCenter * r;
                float startDeg = Mathf.Atan2(a.y - c.y, a.x - c.x) * Mathf.Rad2Deg;
                shape.segments.Add(PathSegment.Arc(c, r, startDeg, turn));
                cursor = p + d2 * t;
            }
            shape.segments.Add(PathSegment.Line(cursor, points[^1]));
            return shape;
        }

        /// <summary>
        /// Ring centered at the origin. gapAngle is the visible gap between the cap tips (0 = closed ring).
        /// The path ends at the cap centers, half a thickness inside the gap edges.
        /// </summary>
        public static PathShape Ring(float radius, float gapCenterDeg, float gapAngleDeg, float thickness)
        {
            var shape = new PathShape();
            if (gapAngleDeg <= 0f)
            {
                shape.closed = true;
                shape.segments.Add(PathSegment.Arc(Vector2.zero, radius, gapCenterDeg, 360f));
                return shape;
            }
            float capDeg = 0.5f * thickness / radius * Mathf.Rad2Deg;
            float start = gapCenterDeg + gapAngleDeg * 0.5f + capDeg;
            float sweep = Mathf.Max(1f, 360f - gapAngleDeg - 2f * capDeg);
            shape.segments.Add(PathSegment.Arc(Vector2.zero, radius, start, sweep));
            return shape;
        }
    }
}
