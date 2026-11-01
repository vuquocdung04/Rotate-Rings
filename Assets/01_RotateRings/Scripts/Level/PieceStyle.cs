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
        [Header("Body")]
        public float thickness = 0.34f;
        [Tooltip("RingFinal.png row: 0 = flat face with rim, 1 = round tube.")]
        [Range(0, 1)] public int styleRow = 0;
        public float pipeCornerRadius = 0.4f;

        [Header("Links")]
        public float stemWidth = 0.3f;
        public Vector2 sleeveSize = new(0.6f, 0.45f);
        public Vector2 sleeveOffset = new(0f, 0.03f);
        public Sprite linkSprite;

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

        public void ApplyTo(Piece p, int colorIndex)
        {
            p.thickness = thickness;
            p.styleRow = styleRow;
            p.color = GetColor(colorIndex);
            p.bodyMaterial = body;
            p.shadowMaterial = shadow;
            p.outlineMaterial = outline;
            p.breakMaterial = breakFx;
            p.sparkMaterial = spark;
        }
    }
}
