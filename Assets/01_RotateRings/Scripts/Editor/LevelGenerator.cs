using System.Collections.Generic;
using UnityEngine;

namespace RotateRings.EditorTools
{
    /// <summary>
    /// Random sample layouts: a closed root ring with a chain or tree of gapped rings linked to it, plus some arms and
    /// straight pipes (held by a link or a clamp). Only the layout is decided here; the level editor then re-rolls the
    /// ring rotations through the solver, so whether a sample is solvable is checked there.
    /// </summary>
    public static class LevelGenerator
    {
        const float Clearance = 1f;        // between a parent's edge and its child's edge
        const float MinSpacing = 0.6f;     // between the edges of unrelated pieces
        const float PipeLength = 3f;
        const float ClampedPipeChance = 0.5f, ArmChance = 0.35f, ChainChance = 0.4f;
        const int MaxChildren = 3, MaxTries = 400;

        static readonly float[] Radii = { 0.75f, 1f, 1.25f, 1.5f };
        static readonly float[] Gaps = { 45f, 60f, 75f, 90f };

        class Node
        {
            public PieceData data;
            public float reach; // bounding radius
            public int children;
            public bool canParent;
        }

        /// <summary>Asks for that many rings and pipes; a crowded layout can end up with fewer.</summary>
        public static LevelData Generate(int rings, int pipes, PieceStyle style)
        {
            var level = new LevelData { name = "sample" };
            var nodes = new List<Node>();
            bool chain = Random.value < ChainChance;
            int ringsLeft = rings, pipesLeft = pipes;

            if (ringsLeft > 0)
            {
                AddRing(level, nodes, style, Vector2.zero, Pick(Radii), 0f);
                ringsLeft--;
            }
            else if (pipesLeft > 0)
            {
                AddPipe(level, nodes, style, null, Vector2.zero, 0f);
                pipesLeft--;
            }

            for (int tries = 0; ringsLeft + pipesLeft > 0 && tries < MaxTries; tries++)
            {
                if (tries == MaxTries / 2) chain = false; // the end of the chain may be boxed in
                var parent = PickParent(nodes, chain);
                bool wantPipe = Random.Range(0, ringsLeft + pipesLeft) < pipesLeft;
                if (parent == null && !wantPipe) break;

                float dirDeg = Random.Range(0, 8) * 45f;
                Vector2 dir = new(Mathf.Cos(dirDeg * Mathf.Deg2Rad), Mathf.Sin(dirDeg * Mathf.Deg2Rad));
                int before = nodes.Count;
                if (wantPipe)
                {
                    TryAddPipe(level, nodes, style, parent, dir, dirDeg);
                    if (nodes.Count > before) pipesLeft--;
                }
                else
                {
                    TryAddRing(level, nodes, style, parent, dir);
                    if (nodes.Count > before) ringsLeft--;
                }
            }

            Recenter(level);
            return level;
        }

        /// <summary>A chain grows from its newest ring, a tree from any ring with room for another child. Pipes never hold anything.</summary>
        static Node PickParent(List<Node> nodes, bool chain)
        {
            var open = nodes.FindAll(n => n.canParent && n.children < MaxChildren);
            if (open.Count == 0) return null;
            return chain ? open[^1] : open[Random.Range(0, open.Count)];
        }

        static void TryAddRing(LevelData level, List<Node> nodes, PieceStyle style, Node parent, Vector2 dir)
        {
            float radius = Pick(Radii);
            Vector2 pos = SnapPos(parent.data.pos + dir * (parent.reach + radius + Clearance));
            if (!Fits(nodes, pos, radius)) return;

            var ring = AddRing(level, nodes, style, pos, radius, Pick(Gaps));
            AddLink(level, style, parent, ring);
            if (Random.value < ArmChance)
                level.arms.Add(new ArmData { piece = ring.data.id, angle = Random.Range(0, 8) * 45f, length = style.armLength });
        }

        /// <summary>Straight pipe tangent to the parent, so it slides past it. With no ring to hold it, it sits by any piece on a clamp.</summary>
        static void TryAddPipe(LevelData level, List<Node> nodes, PieceStyle style, Node parent, Vector2 dir, float dirDeg)
        {
            var anchor = parent ?? nodes[Random.Range(0, nodes.Count)];
            float half = PipeLength * 0.5f;
            Vector2 pos = SnapPos(anchor.data.pos + dir * (anchor.reach + Clearance + (parent == null ? half : 0f)));
            if (Fits(nodes, pos, half, parent)) AddPipe(level, nodes, style, parent, pos, dirDeg);
        }

        static void AddPipe(LevelData level, List<Node> nodes, PieceStyle style, Node parent, Vector2 pos, float rot)
        {
            float half = PipeLength * 0.5f;
            var data = NewPiece(nodes, style, PieceKind.Pipe, pos);
            data.rot = rot;
            data.points = new List<Vector2> { new(0f, -half), new(0f, half) };
            var pipe = Add(level, nodes, data, half, false);

            if (parent == null || Random.value < ClampedPipeChance) level.clamps.Add(new ClampData { piece = data.id, pos = pos });
            else AddLink(level, style, parent, pipe);
        }

        static Node AddRing(LevelData level, List<Node> nodes, PieceStyle style, Vector2 pos, float radius, float gap)
        {
            var data = NewPiece(nodes, style, PieceKind.Ring, pos);
            data.radius = radius;
            data.gapAngle = gap;
            data.gapCenter = 90f;
            return Add(level, nodes, data, radius, true);
        }

        static PieceData NewPiece(List<Node> nodes, PieceStyle style, PieceKind kind, Vector2 pos) => new()
        {
            id = "p" + nodes.Count,
            kind = kind,
            pos = pos,
            color = Random.Range(0, Mathf.Max(1, style.palette.Count)),
            cornerRadius = style.pipeCornerRadius,
        };

        static Node Add(LevelData level, List<Node> nodes, PieceData data, float reach, bool canParent)
        {
            level.pieces.Add(data);
            var node = new Node { data = data, reach = reach, canParent = canParent };
            nodes.Add(node);
            return node;
        }

        static void AddLink(LevelData level, PieceStyle style, Node parent, Node child)
        {
            parent.children++;
            level.links.Add(new LinkData { parent = parent.data.id, child = child.data.id, sleeveOffset = style.sleeveOffset });
        }

        static bool Fits(List<Node> nodes, Vector2 pos, float reach, Node ignore = null)
        {
            foreach (var n in nodes)
                if (n != ignore && Vector2.Distance(pos, n.data.pos) < n.reach + reach + MinSpacing)
                    return false;
            return true;
        }

        static void Recenter(LevelData level)
        {
            if (level.pieces.Count == 0) return;
            Vector2 sum = Vector2.zero;
            foreach (var p in level.pieces) sum += p.pos;
            Vector2 offset = SnapPos(sum / level.pieces.Count);
            foreach (var p in level.pieces) p.pos -= offset;
            foreach (var c in level.clamps) c.pos -= offset;
        }

        static Vector2 SnapPos(Vector2 v) => new(Mathf.Round(v.x * 4f) / 4f, Mathf.Round(v.y * 4f) / 4f);

        static float Pick(float[] values) => values[Random.Range(0, values.Length)];
    }
}
