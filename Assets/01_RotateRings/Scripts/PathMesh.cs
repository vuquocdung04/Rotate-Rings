using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Sweeps a strip along a <see cref="PathShape"/>.
    /// UV0 is 3-sliced to match RingFinal.png (512x256, two 128px rows, capsule 64px thick, 32px cap radius):
    /// head cap U[0..0.125], body U[0.125..0.875], tail cap U[0.875..1]. Other atlases (RingExtra 1024x128) are mapped
    /// onto this layout with the material's tiling/offset.
    /// UV1 = (s / length, 1 on the body / 0 on the caps, length / thickness, 1 if closed): lets the Ring shader
    /// repeat the body texture (wood / marble grain) instead of stretching it.
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
        static readonly List<Vector4> Body = new();
        static readonly List<Color> Colors = new();
        static readonly List<int> Tris = new();

        /// <param name="styleRow">0 = top row of the texture (with inner rim), 1 = bottom row (plain).</param>
        public static void Build(Mesh mesh, PathShape shape, float thickness, int styleRow, Color color,
            float arcStepDeg = 3.75f)
        {
            Verts.Clear(); Normals.Clear(); Tangents.Clear(); Uvs.Clear(); Body.Clear(); Colors.Clear(); Tris.Clear();
            mesh.Clear();
            if (shape == null || shape.segments.Count == 0) return;

            shape.Sample(Points, Tans, Dists, arcStepDeg);
            float length = Mathf.Max(1e-4f, shape.Length);
            float lengthInThickness = length / Mathf.Max(1e-4f, thickness);
            float closed = shape.closed ? 1f : 0f;
            float v0 = styleRow == 0 ? 0.5f : 0f;
            float v1 = v0 + 0.5f;

            // Caps and body don't share vertices (a zero-width quad sits between them), so the body flag never blends.
            if (!shape.closed)
            {
                AddPair(Points[0] - Tans[0] * thickness, Tans[0], 0f, 0f, 0f);
                AddPair(Points[0], Tans[0], HeadU, 0f, 0f);
            }
            for (int i = 0; i < Points.Count; i++)
                AddPair(Points[i], Tans[i], Mathf.Lerp(HeadU, TailU, Dists[i] / length), Dists[i] / length, 1f);
            if (shape.closed)
                AddPair(Points[0], Tans[0], TailU, 1f, 1f); // close the loop on its own vertices: s runs 0..1 with no wrap
            else
            {
                AddPair(Points[^1], Tans[^1], TailU, 1f, 0f);
                AddPair(Points[^1] + Tans[^1] * thickness, Tans[^1], 1f, 1f, 0f);
            }

            int quads = Verts.Count / 2 - 1;
            for (int k = 0; k < quads; k++)
            {
                int i0 = k * 2, o0 = i0 + 1;
                int i1 = i0 + 2, o1 = i1 + 1;
                Tris.Add(i0); Tris.Add(i1); Tris.Add(o1);
                Tris.Add(i0); Tris.Add(o1); Tris.Add(o0);
            }

            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetUVs(0, Uvs);
            mesh.SetUVs(1, Body);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();

            void AddPair(Vector2 p, Vector2 t, float u, float s, float onBody)
            {
                // Right-hand side of the travel direction is "outer" (high V); for a CCW arc that's outward.
                Vector2 side = new(t.y, -t.x);
                Verts.Add(p - side * thickness);
                Uvs.Add(new Vector2(u, v0));
                Verts.Add(p + side * thickness);
                Uvs.Add(new Vector2(u, v1));
                var tangent = new Vector4(t.x, t.y, 0f, 1f);
                var body = new Vector4(s, onBody, lengthInThickness, closed);
                for (int n = 0; n < 2; n++)
                {
                    Normals.Add(Vector3.back);
                    Tangents.Add(tangent);
                    Body.Add(body);
                    Colors.Add(color);
                }
            }
        }
    }
}
