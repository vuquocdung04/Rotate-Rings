using System.Collections.Generic;
using TMPro;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class CurvedTMP : MonoBehaviour
{
    private const int Resolution = 64;

    [SerializeField, Range(-1f, 1f)] private float curvature = 0.2f;
    [SerializeField] private bool shrinkToFit = true;
    [SerializeField] private bool rotateChars = true;
    [SerializeField] private Vector2 offset = Vector2.zero;

    private readonly List<Vector2> samples = new();
    private readonly List<float> distances = new();

    private float totalLength;
    private TMP_Text label;

    public float Curvature
    {
        get => curvature;
        set
        {
            curvature = Mathf.Clamp(value, -1f, 1f);
            Refresh();
        }
    }

    private TMP_Text Label
    {
        get
        {
            if (label == null) label = GetComponent<TMP_Text>();
            return label;
        }
    }

    private void OnEnable()
    {
        Label.OnPreRenderText += Bend;
        Refresh();
    }

    private void OnDisable()
    {
        if (label == null) return;

        label.OnPreRenderText -= Bend;
        Refresh();
    }

    private void OnValidate() => Refresh();

    private void OnRectTransformDimensionsChange() => Refresh();

    public void Refresh()
    {
        if (Label != null) Label.SetVerticesDirty();
    }

    private void Bend(TMP_TextInfo info)
    {
        if (info.characterCount == 0) return;

        RectTransform rect = transform as RectTransform;
        Build(Mathf.Max(rect != null ? rect.rect.width * 0.5f : 0f, 1f));

        if (totalLength <= 0f) return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        bool hasVisible = false;

        for (int c = 0; c < info.characterCount; c++)
        {
            TMP_CharacterInfo character = info.characterInfo[c];
            if (!character.isVisible) continue;

            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;

            for (int k = 0; k < 4; k++)
            {
                Vector3 position = vertices[character.vertexIndex + k];

                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minY = Mathf.Min(minY, position.y);
                maxY = Mathf.Max(maxY, position.y);
            }

            hasVisible = true;
        }

        if (!hasVisible) return;

        float width = Mathf.Max(maxX - minX, 0.0001f);
        float anchorY = (minY + maxY) * 0.5f;
        float midX = (minX + maxX) * 0.5f;
        float scale = shrinkToFit && width > totalLength ? totalLength / width : 1f;

        for (int c = 0; c < info.characterCount; c++)
        {
            TMP_CharacterInfo character = info.characterInfo[c];
            if (!character.isVisible) continue;

            Vector3[] vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            int start = character.vertexIndex;

            float centerX = 0f;
            for (int k = 0; k < 4; k++) centerX += vertices[start + k].x;
            centerX *= 0.25f;

            float t = 0.5f + (centerX - midX) * scale / totalLength;

            Vector2 target = Evaluate(t);

            float sin = 0f;
            float cos = 1f;

            if (rotateChars)
            {
                Vector2 tangent = EvaluateTangent(t);
                float angle = Mathf.Atan2(tangent.y, tangent.x);

                sin = Mathf.Sin(angle);
                cos = Mathf.Cos(angle);
            }

            for (int k = 0; k < 4; k++)
            {
                Vector3 position = vertices[start + k];

                float localX = (position.x - centerX) * scale;
                float localY = (position.y - anchorY) * scale;

                vertices[start + k] = new Vector3(
                    midX + target.x + localX * cos - localY * sin + offset.x,
                    anchorY + target.y + localX * sin + localY * cos + offset.y,
                    position.z);
            }
        }
    }

    private Vector2 Evaluate(float t)
    {
        float target = Mathf.Clamp01(t) * totalLength;
        int index = FindIndex(target);

        float segment = distances[index + 1] - distances[index];
        float lerp = segment > 0f ? (target - distances[index]) / segment : 0f;

        return Vector2.Lerp(samples[index], samples[index + 1], lerp);
    }

    private Vector2 EvaluateTangent(float t)
    {
        int index = FindIndex(Mathf.Clamp01(t) * totalLength);
        Vector2 direction = samples[index + 1] - samples[index];

        return direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }

    private void Build(float halfWidth)
    {
        samples.Clear();
        distances.Clear();
        totalLength = 0f;

        float drop = curvature * halfWidth;

        for (int i = 0; i <= Resolution; i++)
        {
            float u = i / (float)Resolution * 2f - 1f;
            samples.Add(new Vector2(u * halfWidth, -drop * u * u));
        }

        distances.Add(0f);

        for (int i = 1; i < samples.Count; i++)
        {
            totalLength += Vector2.Distance(samples[i - 1], samples[i]);
            distances.Add(totalLength);
        }
    }

    private int FindIndex(float distance)
    {
        int low = 0;
        int high = distances.Count - 1;

        while (low < high - 1)
        {
            int mid = (low + high) >> 1;

            if (distances[mid] <= distance) low = mid;
            else high = mid;
        }

        return Mathf.Clamp(low, 0, samples.Count - 2);
    }
}
