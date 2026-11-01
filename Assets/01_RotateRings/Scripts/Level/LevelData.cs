using System;
using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    public enum PieceKind { Ring, Pipe }

    /// <summary>
    /// One level, saved as JSON (JsonUtility). Shapes are stored with their full parameters, not as preset names,
    /// so editing a preset later never changes (or breaks) existing levels.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public int version = 1;
        public string name;
        public List<PieceData> pieces = new();
        public List<LinkData> links = new();
        public List<ArmData> arms = new();
        public List<ClampData> clamps = new();
    }

    [Serializable]
    public class PieceData
    {
        public string id;
        public PieceKind kind;
        public Vector2 pos;
        public float rot;
        [Tooltip("Index into PieceStyle.palette.")]
        public int color;

        // Ring
        public float radius = 1.25f;
        public float gapAngle = 70f;
        public float gapCenter = 90f;

        // Pipe
        public List<Vector2> points = new();
        public float cornerRadius = 0.4f;
    }

    /// <summary>Parent piece's link gripping the child piece. Placement is automatic (closest points).</summary>
    [Serializable]
    public class LinkData
    {
        public string parent;
        public string child;
        public Vector2 sleeveOffset;
    }

    /// <summary>Rigid rod. Rings use angle (local space); pipes use t along the path and side (+1 right, -1 left).</summary>
    [Serializable]
    public class ArmData
    {
        public string piece;
        public float angle;
        public float t = 0.5f;
        public float side = 1f;
        public float length = 0.9f;
    }

    /// <summary>World-fixed metal clamp on a pipe.</summary>
    [Serializable]
    public class ClampData
    {
        public string piece;
        public Vector2 pos;
    }
}
