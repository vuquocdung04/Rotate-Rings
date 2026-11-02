using System;
using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// The one place for how pieces look. The level builder, loader and editor tool all read from it,
    /// so changing a value here changes every level.
    /// </summary>
    [CreateAssetMenu(fileName = "PieceStyle", menuName = "Rotate Rings/Piece Style")]
    public class PieceStyle : ScriptableObject
    {
        /// <summary>One body finish: its material and the link stem sprite cut from the same atlas.</summary>
        [Serializable]
        public class FinishLook
        {
            public Finish finish;
            public Material body;
            public Sprite linkSprite;
            [Tooltip("RingFinal-style row the atlas expects (RingExtra atlases only have row 0).")]
            [Range(0, 1)] public int styleRow;
        }

        [Header("Body")]
        public float thickness = 0.34f;
        [Tooltip("RingFinal.png row: 0 = flat face with rim, 1 = round tube.")]
        [Range(0, 1)] public int styleRow = 0;
        public float pipeCornerRadius = 0.4f;

        [Header("Finishes (body material + matching link stem)")]
        public List<FinishLook> finishes = new();

        [Header("Links")]
        public float stemWidth = 0.3f;
        [Tooltip("Block sleeve gripping a ring, in the parent's color and finish. x: along the ring, y: along the stem.")]
        public Vector2 sleeveSize = new(0.6f, 0.45f);
        public Vector2 sleeveOffset = new(0f, 0.03f);
        public Sprite linkSprite;
        [Tooltip("Metal clip gripping a pipe (rings get the colored block). Empty = pipes get the block too.")]
        public Sprite sleeveSprite;
        [Tooltip("Untinted material for the metal clip.")]
        public Material sleeveMaterial;
        [Tooltip("Metal clip size. x: along the pipe, y: across it.")]
        public Vector2 clipSize = new(0.41f, 0.57f);

        [Header("Arms")]
        public float armLength = 0.9f;

        [Header("Clamps")]
        public Vector2 clampSize = new(0.56f, 0.72f);
        public Sprite clampSprite;

        [Header("Materials")]
        public Material body;
        public Material shadow;
        public Material outline;
        public Material breakFx;
        public Material spark;
        public Material link;
        public Material clamp;

        [Header("Colors")]
        public List<Color> palette = new();

        public Color GetColor(int index) =>
            palette.Count == 0 ? Color.white : palette[Mathf.Clamp(index, 0, palette.Count - 1)];

        /// <summary>Closest palette entry, used when saving a piece whose color was set by hand.</summary>
        public int NearestColorIndex(Color c)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < palette.Count; i++)
            {
                Color p = palette[i];
                float d = (p.r - c.r) * (p.r - c.r) + (p.g - c.g) * (p.g - c.g) + (p.b - c.b) * (p.b - c.b);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        public FinishLook GetFinish(Finish f) => finishes.Find(x => x.finish == f);

        /// <summary>Stem sprite for links owned by a piece of this finish.</summary>
        public Sprite LinkSpriteFor(Finish f)
        {
            var look = GetFinish(f);
            return look != null && look.linkSprite != null ? look.linkSprite : linkSprite;
        }

        public void ApplyTo(Piece p, int colorIndex, Finish finish = Finish.Plastic)
        {
            p.thickness = thickness;
            p.color = GetColor(colorIndex);
            p.shadowMaterial = shadow;
            p.outlineMaterial = outline;
            p.breakMaterial = breakFx;
            p.sparkMaterial = spark;
            ApplyFinish(p, finish);
        }

        /// <summary>Body material and row of the finish; also restyles the links the piece owns.</summary>
        public void ApplyFinish(Piece p, Finish finish)
        {
            var look = GetFinish(finish);
            p.finish = finish;
            p.bodyMaterial = look != null && look.body != null ? look.body : body;
            p.styleRow = look != null && look.body != null ? look.styleRow : styleRow;
            foreach (var link in p.GetComponentsInChildren<PieceLink>(true))
                if (link.GetComponentInParent<Piece>(true) == p)
                    StyleLink(link, p);
        }

        /// <summary>
        /// Stem and sleeve look of a link (its child must be set). Ring children: a block sleeve in the parent's color and
        /// finish, like the stem. Pipe children: the metal clip, untinted.
        /// </summary>
        public void StyleLink(PieceLink link, Piece parent)
        {
            bool gripsPipe = link.child != null && link.child.shape != null && !link.child.shape.TryGetRingCircle(out _, out _);
            bool metal = sleeveSprite != null && gripsPipe;
            var block = LinkSpriteFor(parent.finish);
            link.metalSleeve = metal;
            link.stemWidth = stemWidth;
            link.sleeveSize = metal ? clipSize : sleeveSize;
            link.sleeveOffset = metal ? Vector2.zero : sleeveOffset;
            SetSprite(link.stem, block, this.link);
            SetSprite(link.sleeve, metal ? sleeveSprite : block, metal ? (sleeveMaterial != null ? sleeveMaterial : clamp) : this.link);
        }

        /// <summary>Sprites with a 9-slice border (link blocks) are drawn sliced so they resize without stretching their ends.</summary>
        static void SetSprite(SpriteRenderer sr, Sprite sprite, Material mat)
        {
            if (sr == null) return;
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.drawMode = sprite != null && sprite.border != Vector4.zero ? SpriteDrawMode.Sliced : SpriteDrawMode.Simple;
        }
    }
}
