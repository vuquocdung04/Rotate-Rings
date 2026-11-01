using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Sweeps a strip along a <see cref="PathShape"/>.
    /// UVs are 3-sliced to match RingFinal.png (512x256, two 128px rows, capsule 64px thick, 32px cap radius):
    /// head cap U[0..0.125], body U[0.125..0.875], tail cap U[0.875..1].
    /// The strip is 2x thickness wide because each texture row is 2x the capsule height.
    /// Open paths get a straight cap extension of one thickness past each end (the visible cap tip is half of it).
    /// </summary>
    public static class PathMesh
    {
        const float HeadU = 0.125f;
        const float TailU = 0.875f;

        static readonly List<Vector2> Points = new();
        static readonly List<Vector2> Tans = new();
        static readonly List<float> Dists = new();

        static readonly List<Vector3> Verts = new();
        static readonly List<Vector3> Normals = new();
        static readonly List<Vector4> Tangents = new();
        static readonly List<Vector2> Uvs = new();
        static readonly List<Color> Colors = new();
        static readonly List<int> Tris = new();

        /// <param name="styleRow">0 = top row of the texture (with inner rim), 1 = bottom row (plain).</param>
        public static void Build(Mesh mesh, PathShape shape, float thickness, int styleRow, Color color,
            float arcStepDeg = 3.75f)
        {
            Verts.Clear(); Normals.Clear(); Tangents.Clear(); Uvs.Clear(); Colors.Clear(); Tris.Clear();
            mesh.Clear();
            if (shape == null || shape.segments.Count == 0) return;

            shape.Sample(Points, Tans, Dists, arcStepDeg);
            float length = Mathf.Max(1e-4f, shape.Length);
            float v0 = styleRow == 0 ? 0.5f : 0f;
            float v1 = v0 + 0.5f;

            if (!shape.closed)
                AddPair(Points[0] - Tans[0] * thickness, Tans[0], 0f);
            for (int i = 0; i < Points.Count; i++)
                AddPair(Points[i], Tans[i], Mathf.Lerp(HeadU, TailU, Dists[i] / length));
            if (!shape.closed)
                AddPair(Points[^1] + Tans[^1] * thickness, Tans[^1], 1f);

            int pairs = Verts.Count / 2;
            int quads = shape.closed ? pairs : pairs - 1;
            for (int k = 0; k < quads; k++)
            {
                int i0 = k * 2, o0 = i0 + 1;
                int i1 = (k + 1) % pairs * 2, o1 = i1 + 1;
                Tris.Add(i0); Tris.Add(i1); Tris.Add(o1);
                Tris.Add(i0); Tris.Add(o1); Tris.Add(o0);
            }

            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();

            void AddPair(Vector2 p, Vector2 t, float u)
            {
                // Right-hand side of the travel direction is "outer" (high V); for a CCW arc that's outward.
                Vector2 side = new(t.y, -t.x);
                Verts.Add(p - side * thickness);
                Uvs.Add(new Vector2(u, v0));
                Verts.Add(p + side * thickness);
                Uvs.Add(new Vector2(u, v1));
                var tangent = new Vector4(t.x, t.y, 0f, 1f);
                for (int n = 0; n < 2; n++)
                {
                    Normals.Add(Vector3.back);
                    Tangents.Add(tangent);
                    Colors.Add(color);
                }
            }
        }
    }
}
