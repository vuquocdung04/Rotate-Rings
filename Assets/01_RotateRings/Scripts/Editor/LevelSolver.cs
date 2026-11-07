using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Offline solver for the level laid out under a root. A snapshot of the scene is simulated with the same rules
    /// as <see cref="Piece"/>:
    /// - Only pieces holding no child move: rings rotate, pipes slide along the straight segment their grips sit on.
    /// - Movement is swept at the game's collision step and stops at the first hit (bodies, links, arms, clamps,
    ///   same probes and skip rules). A piece that starts overlapping ignores collisions for that drag.
    /// - On release, a pipe's grips no longer on the path let go, and any piece whose grips and links are all off at
    ///   once lets go of them too; a piece with no grips and no held children breaks.
    /// Solved = every piece broken. Stops are grouped by which grips they release: one move per releasing run,
    /// plus the far ends of non-releasing runs (moving an arm or link out of someone's way).
    /// </summary>
    public class LevelSolver
    {
        const float RotStep = 2f;       // Piece.collisionStep
        const float SlideStep = 0.04f;  // Piece.collisionStepSlide
        const float ProbeSpacing = 0.08f;
        const float BendTolerance = 0.02f;
        const float LinkProbeSpacing = 0.05f;

        public class Move
        {
            public Piece piece;
            public bool rotate;
            public float amount; // degrees (CCW +) or units along the slide axis
            public readonly List<string> released = new();
            public readonly List<string> broken = new();

            public override string ToString()
            {
                string name = piece != null ? piece.name : "(missing)";
                string what = rotate
                    ? $"rotate {(amount >= 0f ? "CCW" : "CW")} {Mathf.Abs(amount):0}°"
                    : $"slide {amount:+0.00;-0.00}";
                var sb = new StringBuilder($"{name}: {what}");
                if (released.Count > 0) sb.Append("  →  releases ").Append(string.Join(", ", released));
                if (broken.Count > 0) sb.Append("  ·  breaks ").Append(string.Join(", ", broken));
                return sb.ToString();
            }
        }

        public class Result
        {
            public bool solved;
            public bool gaveUp;
            public int states;
            public float seconds;
            public string note;
            public readonly List<Move> moves = new();
            /// <summary>Unsolved: the pieces still standing in the furthest state reached.</summary>
            public readonly List<Piece> stuck = new();
            public readonly List<Piece> overlapping = new();

            public string Summary
            {
                get
                {
                    var sb = new StringBuilder();
                    if (!string.IsNullOrEmpty(note)) sb.AppendLine(note);
                    if (solved) sb.Append($"SOLVABLE in {moves.Count} moves  ·  {states} states, {seconds:0.00}s");
                    else if (gaveUp) sb.Append($"UNKNOWN: search limit hit after {states} states ({seconds:0.00}s). Raise Max sec.");
                    else sb.Append($"NOT SOLVABLE: all {states} reachable states searched ({seconds:0.00}s).");
                    if (!solved && stuck.Count > 0)
                        sb.Append($"\nFurthest reached, still standing: {string.Join(", ", stuck.Where(p => p != null).Select(p => p.name))}");
                    if (overlapping.Count > 0)
                        sb.Append($"\nOverlapping at start: {string.Join(", ", overlapping.Where(p => p != null).Select(p => p.name))}");
                    return sb.ToString();
                }
            }
        }

        // ---------- Snapshot ----------

        struct Pose
        {
            public Vector2 t;
            public float c, s;

            public Pose(Vector2 t, float deg)
            {
                this.t = t;
                float r = deg * Mathf.Deg2Rad;
                c = Mathf.Cos(r);
                s = Mathf.Sin(r);
            }

            public Vector2 Apply(Vector2 v) => new(t.x + c * v.x - s * v.y, t.y + s * v.x + c * v.y);
            public Vector2 Dir(Vector2 v) => new(c * v.x - s * v.y, s * v.x + c * v.y);

            public Vector2 Inverse(Vector2 w)
            {
                float dx = w.x - t.x, dy = w.y - t.y;
                return new Vector2(c * dx + s * dy, -s * dx + c * dy);
            }
        }

        struct Seg
        {
            public Vector2 a, b;
            public float pad;
        }

        class SPiece
        {
            public Piece src;
            public bool ring;
            public PathShape shape;
            public float half;
            public Vector2 pos0;
            public float rot0;
            public float bound; // local radius around the pivot covering everything that collides
            public readonly List<Vector2> probes = new();
            public readonly List<Seg> arms = new();
            public readonly List<int> links = new(); // grips this piece owns
            public readonly List<int> grips = new(); // grips holding this piece
        }

        class SGrip
        {
            public int child;
            public int owner = -1;  // -1: world-fixed clamp
            public Vector2 point;   // owner-local for links, world for clamps
            public Seg stem;        // links, owner-local
            public Vector2 sleeve;  // links, owner-local
            public float sleevePad, clampRadius;
            public float halfLength; // Grip.HalfLength
            public float clearance;  // Grip.ReleaseClearance
            public string label;
        }

        class State
        {
            public bool[] alive, attached;
            public Vector2[] off;
            public float[] ang;

            public State Clone() => new()
            {
                alive = (bool[])alive.Clone(),
                attached = (bool[])attached.Clone(),
                off = (Vector2[])off.Clone(),
                ang = (float[])ang.Clone(),
            };
        }

        readonly List<SPiece> pieces = new();
        readonly List<SGrip> grips = new();
        readonly List<int> clamps = new();
        readonly List<int> near = new();
        readonly State start;

        public LevelSolver(Transform root)
        {
            var index = new Dictionary<Piece, int>();
            foreach (var p in root.GetComponentsInChildren<Piece>())
            {
                if (p.shape == null || p.shape.segments.Count == 0) continue;
                var sp = new SPiece
                {
                    src = p,
                    shape = p.shape,
                    half = p.thickness * 0.5f,
                    pos0 = p.transform.position,
                    rot0 = p.transform.eulerAngles.z,
                    ring = p.shape.TryGetRingCircle(out _, out _),
                };
                p.shape.Sample(sp.probes, null, null, 360f, ProbeSpacing);
                index[p] = pieces.Count;
                pieces.Add(sp);
            }

            foreach (var sp in pieces)
            {
                var pose = new Pose(sp.pos0, sp.rot0);
                foreach (var arm in sp.src.GetComponentsInChildren<PieceArm>())
                {
                    if (arm.GetComponentInParent<Piece>() != sp.src) continue;
                    arm.Layout();
                    arm.GetSegment(out Vector2 a, out Vector2 b, out float pad);
                    sp.arms.Add(new Seg { a = pose.Inverse(a), b = pose.Inverse(b), pad = pad });
                }
            }

            foreach (var link in root.GetComponentsInChildren<PieceLink>())
            {
                var owner = link.GetComponentInParent<Piece>();
                if (owner == null || link.child == null || link.Parent == null) continue;
                if (!index.TryGetValue(owner, out int oi) || !index.TryGetValue(link.child, out int ci)) continue;
                link.Layout();
                link.GetCapsule(out Vector2 a, out Vector2 b, out float stemPad, out float sleevePad);
                var pose = new Pose(pieces[oi].pos0, pieces[oi].rot0);
                AddGrip(new SGrip
                {
                    child = ci,
                    owner = oi,
                    point = pose.Inverse(link.GripWorldPos),
                    stem = new Seg { a = pose.Inverse(a), b = pose.Inverse(b), pad = stemPad },
                    sleeve = pose.Inverse(b),
                    sleevePad = sleevePad,
                    halfLength = link.HalfLength,
                    clearance = link.ReleaseClearance,
                    label = "link of " + owner.name,
                });
            }

            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>())
            {
                if (clamp.child == null || !index.TryGetValue(clamp.child, out int ci)) continue;
                clamp.Layout();
                clamps.Add(grips.Count);
                AddGrip(new SGrip { child = ci, point = clamp.GripWorldPos, clampRadius = clamp.CollisionRadius, halfLength = clamp.HalfLength, clearance = clamp.ReleaseClearance, label = "clamp" });
            }

            foreach (var sp in pieces) sp.bound = Bound(sp);

            int n = pieces.Count;
            start = new State
            {
                alive = Enumerable.Repeat(true, n).ToArray(),
                attached = Enumerable.Repeat(true, grips.Count).ToArray(),
                off = new Vector2[n],
                ang = new float[n],
            };
            // A piece nothing holds and that holds nothing breaks on its first touch.
            Cascade(start, null);
        }

        void AddGrip(SGrip g)
        {
            int gi = grips.Count;
            grips.Add(g);
            pieces[g.child].grips.Add(gi);
            if (g.owner >= 0) pieces[g.owner].links.Add(gi);
        }

        float Bound(SPiece sp)
        {
            float r = 0f;
            foreach (var p in sp.probes) r = Mathf.Max(r, p.magnitude + sp.half);
            foreach (var a in sp.arms) r = Mathf.Max(r, Mathf.Max(a.a.magnitude, a.b.magnitude) + a.pad);
            foreach (int gi in sp.links)
            {
                var g = grips[gi];
                r = Mathf.Max(r, Mathf.Max(g.stem.a.magnitude, g.stem.b.magnitude) + g.stem.pad);
                r = Mathf.Max(r, g.sleeve.magnitude + g.sleevePad);
            }
            return r;
        }

        // ---------- Public API ----------

        /// <summary>Pieces (optionally only those in <paramref name="only"/>) that overlap something in the current layout.</summary>
        public List<Piece> OverlappingAtStart(ICollection<Piece> only = null)
        {
            var list = new List<Piece>();
            var P = Poses(start);
            for (int m = 0; m < pieces.Count; m++)
            {
                if (!start.alive[m] || (only != null && !only.Contains(pieces[m].src))) continue;
                if (Overlaps(m, P, start)) list.Add(pieces[m].src);
            }
            return list;
        }

        public Result Solve(float maxSeconds, int maxStates = 500000)
        {
            var res = new Result();
            res.overlapping.AddRange(OverlappingAtStart());
            var sw = Stopwatch.StartNew();
            var visited = new HashSet<string>();
            int maxDepth = 2 * (pieces.Count + grips.Count) + 16;
            int fewestAlive = int.MaxValue;
            State furthest = start;

            res.solved = Dfs(start, 0);
            res.states = visited.Count;
            res.seconds = (float)sw.Elapsed.TotalSeconds;
            if (!res.solved)
                for (int i = 0; i < pieces.Count; i++)
                    if (furthest.alive[i]) res.stuck.Add(pieces[i].src);
            return res;

            bool Dfs(State s, int depth)
            {
                int alive = s.alive.Count(a => a);
                if (alive == 0) return true;
                if (alive < fewestAlive)
                {
                    fewestAlive = alive;
                    furthest = s;
                }
                if (depth >= maxDepth) return false;
                if (visited.Count >= maxStates || sw.Elapsed.TotalSeconds > maxSeconds)
                {
                    res.gaveUp = true;
                    return false;
                }
                if (!visited.Add(Key(s))) return false;

                foreach (var (child, move) in Expand(s))
                {
                    res.moves.Add(move);
                    if (Dfs(child, depth + 1)) return true;
                    res.moves.RemoveAt(res.moves.Count - 1);
                    if (res.gaveUp) return false;
                }
                return false;
            }
        }

        // ---------- Search ----------

        string Key(State s)
        {
            var sb = new StringBuilder(pieces.Count * 8 + grips.Count);
            foreach (bool a in s.alive) sb.Append(a ? '1' : '0');
            sb.Append('|');
            foreach (bool a in s.attached) sb.Append(a ? '1' : '0');
            int turn = Mathf.RoundToInt(360f / RotStep);
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!s.alive[i]) continue;
                if (pieces[i].ring)
                    sb.Append('|').Append(Mathf.RoundToInt(Mathf.Repeat(s.ang[i], 360f) / RotStep) % turn);
                else
                    sb.Append('|').Append(Mathf.RoundToInt(s.off[i].x / SlideStep)).Append(',').Append(Mathf.RoundToInt(s.off[i].y / SlideStep));
            }
            return sb.ToString();
        }

        /// <summary>Every move from this state; releasing moves first (most releases first), then repositions.</summary>
        List<(State, Move)> Expand(State s)
        {
            var P = Poses(s);
            var found = new List<(State state, Move move, int score)>();
            var seq = new List<(int k, ulong mask)>();

            for (int m = 0; m < pieces.Count; m++)
            {
                if (!s.alive[m] || HoldsChild(m, s) || !Motion(m, P, s, out bool rotate, out Vector2 axis)) continue;
                var sp = pieces[m];
                Pose basePose = P[m];
                bool ignore = Overlaps(m, P, s);
                float step = rotate ? RotStep : SlideStep;
                int maxK = rotate ? Mathf.CeilToInt(180f / RotStep) : Mathf.CeilToInt((sp.shape.Length + 1f) / SlideStep);

                seq.Clear();
                for (int k = 1; k <= maxK && TryPose(m, -k * step, rotate, axis, P, s, ignore, out ulong mask); k++)
                    seq.Add((-k, mask));
                seq.Reverse();
                P[m] = basePose;
                seq.Add((0, ReleaseMask(m, P, s)));
                for (int k = 1; k <= maxK && TryPose(m, k * step, rotate, axis, P, s, ignore, out ulong mask); k++)
                    seq.Add((k, mask));
                P[m] = basePose;

                // Runs of poses releasing the same grips.
                int i0 = 0;
                for (int i = 1; i <= seq.Count; i++)
                {
                    if (i < seq.Count && seq[i].mask == seq[i0].mask) continue;
                    int i1 = i - 1;
                    ulong runMask = seq[i0].mask;
                    if (runMask != 0)
                        found.Add(MakeMove(s, m, rotate, axis, seq[(i0 + i1) / 2].k * step, runMask));
                    else
                    {
                        if (seq[i0].k != 0) found.Add(MakeMove(s, m, rotate, axis, seq[i0].k * step, 0));
                        if (i1 != i0 && seq[i1].k != 0) found.Add(MakeMove(s, m, rotate, axis, seq[i1].k * step, 0));
                    }
                    i0 = i;
                }
            }

            return found.OrderByDescending(f => f.score).Select(f => (f.state, f.move)).ToList();
        }

        (State, Move, int) MakeMove(State s, int m, bool rotate, Vector2 axis, float amount, ulong mask)
        {
            var sp = pieces[m];
            var c = s.Clone();
            if (rotate) c.ang[m] += amount;
            else c.off[m] += axis * amount;

            var move = new Move { piece = sp.src, rotate = rotate, amount = amount };
            for (int j = 0; j < sp.grips.Count && j < 64 && !sp.ring; j++)
            {
                if ((mask & (1UL << j)) == 0) continue;
                c.attached[sp.grips[j]] = false;
                move.released.Add(grips[sp.grips[j]].label);
            }
            Settle(c, move);
            int score = move.released.Count * 1000 + move.broken.Count * 100;
            return (c, move, score);
        }

        /// <summary>Mirrors Piece.SettleAll: a piece goes once its grips and its links are all off at the same time.</summary>
        void Settle(State s, Move move)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                Cascade(s, move.broken);
                var P = Poses(s);
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (!s.alive[i] || !CanLetGo(i, P, s)) continue;
                    foreach (int gi in pieces[i].grips.Concat(pieces[i].links))
                    {
                        if (!s.attached[gi]) continue;
                        s.attached[gi] = false;
                        move.released.Add(grips[gi].label);
                    }
                    changed = true;
                }
            }
        }

        bool CanLetGo(int i, Pose[] P, State s)
        {
            bool tied = false;
            foreach (int gi in pieces[i].grips.Concat(pieces[i].links))
            {
                if (!s.attached[gi]) continue;
                if (!GripOff(gi, P)) return false;
                tied = true;
            }
            return tied;
        }

        bool GripOff(int gi, Pose[] P)
        {
            var g = grips[gi];
            var c = pieces[g.child];
            return !c.shape.closed && c.shape.Distance(P[g.child].Inverse(GripWorld(gi, P))) > c.half + g.clearance;
        }

        /// <summary>Breaks every piece with no grips left and no held children, until nothing changes.</summary>
        void Cascade(State s, List<string> broken)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (!s.alive[i] || IsGripped(i, s) || HoldsChild(i, s)) continue;
                    s.alive[i] = false;
                    broken?.Add(pieces[i].src.name);
                    changed = true;
                }
            }
        }

        bool IsGripped(int i, State s)
        {
            foreach (int gi in pieces[i].grips)
                if (s.attached[gi]) return true;
            return false;
        }

        bool HoldsChild(int i, State s)
        {
            foreach (int gi in pieces[i].links)
                if (s.attached[gi]) return true;
            return false;
        }

        // ---------- Motion (mirrors Piece.ResolveMotion / GripsAllowPose / CheckGrips) ----------

        Pose[] Poses(State s)
        {
            var P = new Pose[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
                P[i] = new Pose(pieces[i].pos0 + s.off[i], pieces[i].rot0 + s.ang[i]);
            return P;
        }

        Vector2 GripWorld(int gi, Pose[] P)
        {
            var g = grips[gi];
            return g.owner >= 0 ? P[g.owner].Apply(g.point) : g.point;
        }

        bool Motion(int m, Pose[] P, State s, out bool rotate, out Vector2 axis)
        {
            var sp = pieces[m];
            rotate = sp.ring;
            axis = Vector2.zero;
            if (rotate) return true;
            foreach (int gi in sp.grips)
            {
                if (!s.attached[gi]) continue;
                int i = sp.shape.ClosestSegmentIndex(P[m].Inverse(GripWorld(gi, P)), out _);
                var seg = sp.shape.segments[i];
                if (seg.type != SegmentType.Line) return false;
                Vector2 dir = P[m].Dir(seg.b - seg.a).normalized;
                if (axis == Vector2.zero) axis = dir;
                else if (Mathf.Abs(axis.x * dir.y - axis.y * dir.x) > 0.01f) return false;
            }
            return axis != Vector2.zero;
        }

        bool TryPose(int m, float amount, bool rotate, Vector2 axis, Pose[] P, State s, bool ignoreCollision, out ulong mask)
        {
            var sp = pieces[m];
            P[m] = rotate
                ? new Pose(sp.pos0 + s.off[m], sp.rot0 + s.ang[m] + amount)
                : new Pose(sp.pos0 + s.off[m] + axis * amount, sp.rot0 + s.ang[m]);
            mask = 0;
            if (!rotate && !GripsAllowPose(m, axis, P, s)) return false;
            if (!ignoreCollision && Overlaps(m, P, s)) return false;
            mask = ReleaseMask(m, P, s);
            return true;
        }

        bool GripsAllowPose(int m, Vector2 axis, Pose[] P, State s)
        {
            var sp = pieces[m];
            foreach (int gi in sp.grips)
            {
                if (!s.attached[gi]) continue;
                Vector2 c = GripWorld(gi, P), h = axis * grips[gi].halfLength;
                if (!OnStraight(sp, P[m], c) || !OnStraight(sp, P[m], c + h) || !OnStraight(sp, P[m], c - h)) return false;
            }
            return true;
        }

        static bool OnStraight(SPiece sp, Pose pose, Vector2 world)
        {
            Vector2 local = pose.Inverse(world);
            return sp.shape.DistanceToLines(local) <= BendTolerance || sp.shape.IsEndPoint(sp.shape.ClosestPoint(local, out _));
        }

        /// <summary>
        /// Grips (bit = index in the piece's grip list) off the path at this pose. Pipes let those go; a ring only parks
        /// here, since another ring may need it out of a shared parent's link, so each mask is a stop worth trying.
        /// </summary>
        ulong ReleaseMask(int m, Pose[] P, State s)
        {
            var sp = pieces[m];
            ulong mask = 0;
            for (int j = 0; j < sp.grips.Count && j < 64; j++)
                if (s.attached[sp.grips[j]] && GripOff(sp.grips[j], P)) mask |= 1UL << j;
            return mask;
        }

        // ---------- Collision (mirrors Piece.Overlaps / ProbeHits) ----------

        bool Overlaps(int m, Pose[] P, State s)
        {
            var sp = pieces[m];
            var pm = P[m];

            near.Clear();
            for (int o = 0; o < pieces.Count; o++)
            {
                if (o == m || !s.alive[o]) continue;
                float r = pieces[o].bound + sp.bound;
                if ((P[o].t - pm.t).sqrMagnitude <= r * r) near.Add(o);
            }

            foreach (var lp in sp.probes)
                if (ProbeHits(m, pm.Apply(lp), sp.half, -1, P, s)) return true;

            foreach (int gi in sp.links)
            {
                var g = grips[gi];
                Vector2 a = pm.Apply(g.stem.a), b = pm.Apply(g.stem.b);
                if (SegmentHits(m, a, b, g.stem.pad, gi, P, s) || ProbeHits(m, pm.Apply(g.sleeve), g.sleevePad, gi, P, s)) return true;
            }

            foreach (var arm in sp.arms)
                if (SegmentHits(m, pm.Apply(arm.a), pm.Apply(arm.b), arm.pad, -1, P, s)) return true;

            return false;
        }

        bool SegmentHits(int m, Vector2 a, Vector2 b, float pad, int fromLink, Pose[] P, State s)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / LinkProbeSpacing));
            for (int i = 0; i <= n; i++)
                if (ProbeHits(m, Vector2.Lerp(a, b, (float)i / n), pad, fromLink, P, s)) return true;
            return false;
        }

        /// <param name="fromLink">Grip index of the moving piece's link the probe belongs to, or -1 for body/arm probes.</param>
        bool ProbeHits(int m, Vector2 p, float pad, int fromLink, Pose[] P, State s)
        {
            foreach (int o in near)
            {
                var so = pieces[o];
                var po = P[o];
                float r = so.bound + pad;
                if ((p - po.t).sqrMagnitude > r * r) continue;

                bool skipBody = fromLink >= 0 && s.attached[fromLink] && grips[fromLink].child == o;
                if (!skipBody && so.shape.Distance(po.Inverse(p)) < pad + so.half) return true;

                foreach (int gi in so.links)
                {
                    var g = grips[gi];
                    if (fromLink < 0 && s.attached[gi] && g.child == m) continue; // grips the mover by design
                    if (SegmentDistance(p, po.Apply(g.stem.a), po.Apply(g.stem.b)) < pad + g.stem.pad) return true;
                    if (Vector2.Distance(p, po.Apply(g.sleeve)) < pad + g.sleevePad) return true;
                }

                foreach (var arm in so.arms)
                    if (SegmentDistance(p, po.Apply(arm.a), po.Apply(arm.b)) < pad + arm.pad) return true;
            }

            foreach (int gi in clamps)
            {
                var g = grips[gi];
                if (!s.attached[gi] || g.child == m) continue;
                if (Vector2.Distance(p, g.point) < pad + g.clampRadius) return true;
            }
            return false;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            return Vector2.Distance(p, a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2));
        }
    }
}
