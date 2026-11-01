using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Builds a C-shaped ring mesh: a quad strip bent along an arc.
    /// UVs are 3-sliced to match RingFinal.png (512x256, two 128px rows, capsule 64px thick, 32px cap radius):
    /// head cap U[0..0.125], body U[0.125..0.875], tail cap U[0.875..1].
    /// The strip is 2x thickness wide because each texture row is 2x the capsule height.
    /// </summary>
    public static class ArcRingMesh
    {
        const float HeadU = 0.125f;
        const float TailU = 0.875f;

        static readonly List<Vector3> Verts = new();
        static readonly List<Vector3> Normals = new();
        static readonly List<Vector4> Tangents = new();
        static readonly List<Vector2> Uvs = new();
        static readonly List<Color> Colors = new();
        static readonly List<int> Tris = new();

        /// <param name="gapAngle">Visible gap between the two cap tips, degrees.</param>
        /// <param name="gapCenter">Local angle of the gap center, degrees (90 = up).</param>
        /// <param name="styleRow">0 = top row of the texture (with inner rim), 1 = bottom row (plain).</param>
        public static void Build(Mesh mesh, float radius, float thickness, float gapAngle, float gapCenter,
            int segments, int styleRow, Color color)
        {
            Verts.Clear(); Normals.Clear(); Tangents.Clear(); Uvs.Clear(); Colors.Clear(); Tris.Clear();

            float capRad = 0.5f * thickness / radius;   // angular size of the rounded cap
            float padRad = thickness / radius;          // angular length of the head/tail UV slice
            float gapHalf = gapAngle * 0.5f * Mathf.Deg2Rad;
            float gc = gapCenter * Mathf.Deg2Rad;
            float bodyStart = gc + gapHalf + capRad;
            float bodyEnd = gc + Mathf.PI * 2f - gapHalf - capRad;
            if (bodyEnd <= bodyStart) bodyEnd = bodyStart + 0.01f;

            float rIn = Mathf.Max(0.001f, radius - thickness);
            float rOut = radius + thickness;
            float v0 = styleRow == 0 ? 0.5f : 0f;
            float v1 = v0 + 0.5f;

            int capSeg = Mathf.Max(3, segments / 16);
            for (int i = 0; i <= capSeg; i++)
            {
                float t = (float)i / capSeg;
                AddPair(bodyStart - padRad * (1f - t), Mathf.Lerp(0f, HeadU, t));
            }
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                AddPair(Mathf.Lerp(bodyStart, bodyEnd, t), Mathf.Lerp(HeadU, TailU, t));
            }
            for (int i = 1; i <= capSeg; i++)
            {
                float t = (float)i / capSeg;
                AddPair(bodyEnd + padRad * t, Mathf.Lerp(TailU, 1f, t));
            }

            int pairs = Verts.Count / 2;
            for (int k = 0; k < pairs - 1; k++)
            {
                int i0 = k * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                Tris.Add(i0); Tris.Add(i1); Tris.Add(o1);
                Tris.Add(i0); Tris.Add(o1); Tris.Add(o0);
            }

            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();

            void AddPair(float angle, float u)
            {
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                // Tangent follows increasing U (counter-clockwise), bitangent points outward (increasing V).
                var tangent = new Vector4(-s, c, 0f, 1f);
                Verts.Add(new Vector3(c * rIn, s * rIn, 0f));
                Uvs.Add(new Vector2(u, v0));
                Verts.Add(new Vector3(c * rOut, s * rOut, 0f));
                Uvs.Add(new Vector2(u, v1));
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
