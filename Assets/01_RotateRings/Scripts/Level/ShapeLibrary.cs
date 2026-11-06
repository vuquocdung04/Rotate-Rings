using System;
using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>A palette entry for the level editor (and later the generator). Placing one copies its parameters.</summary>
    [Serializable]
    public class ShapePreset
    {
        public string name;
        public PieceKind kind;

        [Header("Ring")]
        public float radius = 1.25f;
        [Tooltip("0 = closed ring.")]
        public float gapAngle = 70f;

        [Header("Pipe")]
        public List<Vector2> points = new();
    }

    [CreateAssetMenu(fileName = "ShapeLibrary", menuName = "Rotate Rings/Shape Library")]
    public class ShapeLibrary : ScriptableObject
    {
        public List<ShapePreset> presets = new();

        public void ResetToDefaults()
        {
            presets = new List<ShapePreset>
            {
                // One of each: radius, gap and pipe corners are dragged on the canvas.
                Ring("Ring", 1.25f, 0f),
                Pipe("Pipe", new(0f, -1.5f), new(0f, 1.5f)),
            };
        }

        static ShapePreset Ring(string name, float radius, float gap) =>
            new() { name = name, kind = PieceKind.Ring, radius = radius, gapAngle = gap };

        static ShapePreset Pipe(string name, params Vector2[] pts) =>
            new() { name = name, kind = PieceKind.Pipe, points = new List<Vector2>(pts) };
    }
}
