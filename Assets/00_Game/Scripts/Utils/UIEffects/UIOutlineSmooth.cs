using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Outline Smooth")]
public class UIOutlineSmooth : BaseMeshEffect
{
    [SerializeField] private Color color = new Color(0.4f, 0.18f, 0.1f, 1f);
    [SerializeField, Range(0f, 12f)] private float size = 4f;
    [SerializeField, Range(4, 16)] private int samples = 12;

    [Header("Shadow")]
    [SerializeField] private Color shadowColor = new Color(0.25f, 0.1f, 0.05f, 0.6f);
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -5f);

    [SerializeField] private bool useGraphicAlpha = true;

    private readonly List<UIVertex> source = new List<UIVertex>();
    private readonly List<UIVertex> output = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
            return;

        source.Clear();
        output.Clear();
        vh.GetUIVertexStream(source);

        bool shadow = shadowColor.a > 0f && shadowOffset != Vector2.zero;

        if (shadow)
            AddRing(shadowOffset, shadowColor);

        if (size > 0f)
            AddRing(Vector2.zero, color);

        output.AddRange(source);

        vh.Clear();
        vh.AddUIVertexTriangleStream(output);
    }

    private void AddRing(Vector2 center, Color tint)
    {
        int count = size > 0f ? Mathf.Max(4, samples) : 1;

        for (int s = 0; s < count; s++)
        {
            float angle = s * Mathf.PI * 2f / count;
            Vector3 offset = size > 0f
                ? new Vector3(center.x + Mathf.Cos(angle) * size, center.y + Mathf.Sin(angle) * size, 0f)
                : (Vector3)center;

            for (int i = 0; i < source.Count; i++)
            {
                UIVertex v = source[i];
                Color32 original = v.color;

                v.position += offset;
                v.color = new Color(tint.r, tint.g, tint.b, tint.a * (useGraphicAlpha ? original.a / 255f : 1f));

                output.Add(v);
            }
        }
    }

    public void SetColor(Color value)
    {
        color = value;
        Refresh();
    }

    private void Refresh()
    {
        if (graphic != null)
            graphic.SetVerticesDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        Refresh();
    }
#endif
}
