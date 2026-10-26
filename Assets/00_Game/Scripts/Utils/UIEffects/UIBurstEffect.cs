using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIBurstEffect : MonoBehaviour
{
    [SerializeField] private List<Sprite> sprites = new List<Sprite>();
    [SerializeField] private int count = 14;
    [SerializeField] private Vector2 distanceRange = new Vector2(140f, 280f);
    [SerializeField] private Vector2 sizeRange = new Vector2(36f, 72f);
    [SerializeField] private Vector2 durationRange = new Vector2(0.5f, 0.8f);
    [SerializeField] private float gravity = 120f;
    [SerializeField] private float spin = 360f;

    private readonly Stack<Image> _pool = new Stack<Image>();
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
    }

    public void Play(Vector3 worldPosition)
    {
        if (sprites.Count == 0)
            return;

        Vector2 center = _rectTransform.InverseTransformPoint(worldPosition);
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = (step * i + Random.Range(-step, step) * 0.4f) * Mathf.Deg2Rad;
            float distance = Random.Range(distanceRange.x, distanceRange.y);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Spawn(center, center + direction * distance);
        }
    }

    private void Spawn(Vector2 from, Vector2 to)
    {
        Image image = Get();
        RectTransform rt = image.rectTransform;

        float size = Random.Range(sizeRange.x, sizeRange.y);
        float duration = Random.Range(durationRange.x, durationRange.y);

        image.sprite = sprites[Random.Range(0, sprites.Count)];
        image.color = Color.white;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = from;
        rt.localScale = Vector3.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetLink(image.gameObject);

        sequence.Insert(0f, rt.DOScale(1f, duration * 0.3f).SetEase(Ease.OutBack));
        sequence.Insert(0f, DOVirtual.Float(0f, 1f, duration, t =>
        {
            float k = 1f - (1f - t) * (1f - t) * (1f - t);
            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k) + Vector2.down * (gravity * t * t);
        }).SetEase(Ease.Linear));
        sequence.Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-spin, spin)), duration, RotateMode.LocalAxisAdd));
        sequence.Insert(duration * 0.6f, rt.DOScale(0f, duration * 0.4f).SetEase(Ease.InBack));
        sequence.Insert(duration * 0.6f, image.DOFade(0f, duration * 0.4f));
        sequence.OnKill(() => Release(image));
    }

    private Image Get()
    {
        Image image = _pool.Count > 0 ? _pool.Pop() : Create();

        image.gameObject.SetActive(true);
        image.transform.SetAsLastSibling();

        return image;
    }

    private Image Create()
    {
        GameObject go = new GameObject("Particle", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        return image;
    }

    private void Release(Image image)
    {
        if (image == null)
            return;

        image.gameObject.SetActive(false);
        _pool.Push(image);
    }
}
