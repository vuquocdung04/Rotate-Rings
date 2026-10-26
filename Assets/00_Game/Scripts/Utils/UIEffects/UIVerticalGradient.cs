using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Vertical Gradient")]
public class UIVerticalGradient : BaseMeshEffect
{
    [SerializeField] private Color top = Color.white;
    [SerializeField] private Color bottom = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("Where the blend starts and ends, as 0 (bottom) .. 1 (top) of the mesh height.")]
    [SerializeField] private Vector2 range = new Vector2(0.1f, 0.9f);

    private readonly List<UIVertex> verts = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        verts.Clear();
        vh.GetUIVertexStream(verts);

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < verts.Count; i++)
        {
            float y = verts[i].position.y;
            min = Mathf.Min(min, y);
            max = Mathf.Max(max, y);
        }

        float height = Mathf.Max(0.0001f, max - min);

        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            float t = Mathf.InverseLerp(range.x, range.y, (v.position.y - min) / height);

            v.color = (Color)v.color * Color.Lerp(bottom, top, t);
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
