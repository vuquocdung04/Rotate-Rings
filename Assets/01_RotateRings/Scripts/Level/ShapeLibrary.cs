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
                Ring("C 70", 1.25f, 70f),
                Ring("C 40", 1.25f, 40f),
                Ring("C 30", 1.25f, 30f),
                Ring("U 120", 1.25f, 120f),
                Ring("Closed", 1.25f, 0f),
                Ring("Small C", 0.8f, 70f),
                Ring("Small Closed", 0.8f, 0f),
                Ring("Big C", 2.4f, 40f),
                Ring("Big Closed", 2.4f, 0f),

                Pipe("I 2", new(0f, -1f), new(0f, 1f)),
                Pipe("I 3", new(0f, -1.5f), new(0f, 1.5f)),
                Pipe("I 4", new(0f, -2f), new(0f, 2f)),
                Pipe("L 2x1", new(-0.75f, 1f), new(-0.75f, -0.5f), new(0.75f, -0.5f)),
                Pipe("L 3x1.5", new(-1f, 1.5f), new(-1f, -0.75f), new(1f, -0.75f)),
                Pipe("U pipe", new(-0.8f, 0.8f), new(-0.8f, -0.8f), new(0.8f, -0.8f), new(0.8f, 0.8f)),
                Pipe("S pipe", new(-1.2f, 1.2f), new(-1.2f, 0f), new(1.2f, 0f), new(1.2f, -1.2f)),
                Pipe("Hook", new(-0.6f, 1.2f), new(-0.6f, -0.8f), new(0.6f, -0.8f), new(0.6f, 0f)),
            };
        }

        static ShapePreset Ring(string name, float radius, float gap) =>
            new() { name = name, kind = PieceKind.Ring, radius = radius, gapAngle = gap };

        static ShapePreset Pipe(string name, params Vector2[] pts) =>
            new() { name = name, kind = PieceKind.Pipe, points = new List<Vector2>(pts) };
    }
}
