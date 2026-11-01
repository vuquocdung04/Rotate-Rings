using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Builds pieces, links, arms and clamps from data, and exports a scene hierarchy back to data.
    /// Shared by the runtime LevelLoader and the editor tool, so both produce exactly the same objects.
    /// </summary>
    public static class LevelBuilder
    {
        // ---------- Data -> scene ----------

        public static Dictionary<string, Piece> Build(LevelData level, PieceStyle style, Transform root)
        {
            // In play mode, wire everything while inactive so links/clamps register their grips on OnEnable.
            bool wasActive = root.gameObject.activeSelf;
            if (Application.isPlaying) root.gameObject.SetActive(false);

            var map = new Dictionary<string, Piece>();
            foreach (var pd in level.pieces)
                map[pd.id] = CreatePiece(pd, style, root);

            foreach (var ld in level.links)
                if (map.TryGetValue(ld.parent, out var parent) && map.TryGetValue(ld.child, out var child))
                    CreateLink(parent, child, style, ld.sleeveOffset);

            foreach (var ad in level.arms)
                if (map.TryGetValue(ad.piece, out var owner))
                    CreateArm(owner, ad.angle, ad.length, ad.t, ad.side);

            foreach (var cd in level.clamps)
                if (map.TryGetValue(cd.piece, out var pipe))
                    CreateClamp(pipe, cd.pos, style, root);

            AssignSortingOrders(root);
            root.gameObject.SetActive(wasActive);
            return map;
        }

        public static Piece CreatePiece(PieceData d, PieceStyle style, Transform root)
        {
            var go = new GameObject(string.IsNullOrEmpty(d.id) ? d.kind.ToString() : d.id);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(d.pos.x, d.pos.y, 0f), Quaternion.Euler(0f, 0f, d.rot));

            var p = go.AddComponent<Piece>();
            style.ApplyTo(p, d.color);
            if (d.kind == PieceKind.Ring)
            {
                var ring = go.AddComponent<RingShape>();
                ring.radius = d.radius;
                ring.gapAngle = d.gapAngle;
                ring.gapCenter = d.gapCenter;
                ring.Apply();
            }
            else
            {
                var pipe = go.AddComponent<PipeShape>();
                pipe.points = new List<Vector2>(d.points);
                pipe.cornerRadius = d.cornerRadius;
                pipe.Apply();
            }
            return p;
        }

        public static PieceLink CreateLink(Piece parent, Piece child, PieceStyle style, Vector2 sleeveOffset)
        {
            var go = new GameObject($"Link_{child.name}");
            go.transform.SetParent(parent.transform, false);
            var link = go.AddComponent<PieceLink>();
            link.child = child;
            link.stemWidth = style.stemWidth;
            link.sleeveSize = style.sleeveSize;
            link.sleeveOffset = sleeveOffset;
            link.stem = MakeSprite("Stem", go.transform, style.linkSprite, style.link);
            link.sleeve = MakeSprite("Sleeve", go.transform, style.linkSprite, style.link);
            link.Layout();
            return link;
        }

        /// <param name="angle">Rings: local angle.</param>
        /// <param name="t">Pipes: position along the path (0..1).</param>
        /// <param name="side">Pipes: +1 right of the path direction, -1 left.</param>
        public static PieceArm CreateArm(Piece owner, float angle, float length, float t = 0.5f, float side = 1f)
        {
            var go = new GameObject("Arm");
            go.transform.SetParent(owner.transform, false);
            var arm = go.AddComponent<PieceArm>();
            arm.angle = angle;
            arm.pathT = t;
            arm.side = side;
            arm.length = length;
            arm.Layout();
            return arm;
        }

        public static PipeClamp CreateClamp(Piece pipe, Vector2 pos, PieceStyle style, Transform root)
        {
            var go = new GameObject($"Clamp_{pipe.name}");
            go.transform.SetParent(root, false);
            go.transform.position = pos;
            var clamp = go.AddComponent<PipeClamp>();
            clamp.child = pipe;
            clamp.size = style.clampSize;
            clamp.sprite = MakeSprite("Sprite", go.transform, style.clampSprite, style.clamp);
            clamp.Layout();
            return clamp;
        }

        /// <summary>Sprites with a 9-slice border (LinkBar) are drawn sliced so they resize without stretching.</summary>
        static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            if (sprite != null && sprite.border != Vector4.zero) sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        /// <summary>
        /// Draw order from the link tree: a child draws above its parent (depth * 10), so sleeves sit on the right ring.
        /// Re-lays out every piece afterwards since link/arm/clamp orders depend on it.
        /// </summary>
        public static void AssignSortingOrders(Transform root)
        {
            var pieces = root.GetComponentsInChildren<Piece>(true);
            var parentsOf = new Dictionary<Piece, List<Piece>>();
            foreach (var link in root.GetComponentsInChildren<PieceLink>(true))
            {
                var parent = link.GetComponentInParent<Piece>(true);
                if (parent == null || link.child == null) continue;
                if (!parentsOf.TryGetValue(link.child, out var list)) parentsOf[link.child] = list = new List<Piece>();
                list.Add(parent);
            }

            var depth = new Dictionary<Piece, int>();
            foreach (var p in pieces) Depth(p, parentsOf, depth, 0);
            // All orders first: link layout reads both its parent's and child's order.
            foreach (var p in pieces) p.sortingOrder = depth[p] * 10;
            foreach (var p in pieces) p.Rebuild();
            foreach (var c in root.GetComponentsInChildren<PipeClamp>(true)) c.Layout();
        }

        static int Depth(Piece p, Dictionary<Piece, List<Piece>> parentsOf, Dictionary<Piece, int> memo, int guard)
        {
            if (memo.TryGetValue(p, out int d)) return d;
            d = 0;
            if (guard < 64 && parentsOf.TryGetValue(p, out var parents))
                foreach (var parent in parents)
                    d = Mathf.Max(d, Depth(parent, parentsOf, memo, guard + 1) + 1);
            memo[p] = d;
            return d;
        }

        // ---------- Scene -> data ----------

        public static LevelData Export(Transform root, PieceStyle style, string levelName)
        {
            var level = new LevelData { name = levelName };
            var ids = new Dictionary<Piece, string>();
            var pieces = root.GetComponentsInChildren<Piece>(true);
            for (int i = 0; i < pieces.Length; i++) ids[pieces[i]] = "p" + i;

            foreach (var p in pieces)
            {
                var t = p.transform;
                var pd = new PieceData
                {
                    id = ids[p],
                    pos = Round(t.position),
                    rot = Round(Mathf.Repeat(t.eulerAngles.z, 360f)),
                    color = style.NearestColorIndex(p.color),
                };
                if (p.TryGetComponent<RingShape>(out var ring))
                {
                    pd.kind = PieceKind.Ring;
                    pd.radius = Round(ring.radius);
                    pd.gapAngle = Round(ring.gapAngle);
                    pd.gapCenter = Round(ring.gapCenter);
                }
                else if (p.TryGetComponent<PipeShape>(out var pipe))
                {
                    pd.kind = PieceKind.Pipe;
                    foreach (var pt in pipe.points) pd.points.Add(Round(pt));
                    pd.cornerRadius = Round(pipe.cornerRadius);
                }
                level.pieces.Add(pd);

                foreach (var link in p.GetComponentsInChildren<PieceLink>(true))
                {
                    if (link.GetComponentInParent<Piece>(true) != p) continue;
                    if (link.child == null || !ids.TryGetValue(link.child, out var childId)) continue;
                    level.links.Add(new LinkData { parent = ids[p], child = childId, sleeveOffset = Round(link.sleeveOffset) });
                }
                foreach (var arm in p.GetComponentsInChildren<PieceArm>(true))
                    if (arm.GetComponentInParent<Piece>(true) == p)
                        level.arms.Add(new ArmData
                        {
                            piece = ids[p], angle = Round(arm.angle), t = Round(arm.pathT), side = arm.side, length = Round(arm.length),
                        });
            }

            foreach (var clamp in root.GetComponentsInChildren<PipeClamp>(true))
                if (clamp.child != null && ids.TryGetValue(clamp.child, out var pipeId))
                    level.clamps.Add(new ClampData { piece = pipeId, pos = Round(clamp.transform.position) });

            return level;
        }

        static float Round(float v) => Mathf.Round(v * 1000f) / 1000f;
        static Vector2 Round(Vector2 v) => new(Round(v.x), Round(v.y));
        static Vector2 Round(Vector3 v) => new(Round(v.x), Round(v.y));
    }
}
