using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UISweepLoop : MonoBehaviour
{
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float interval = 1.8f;
    [SerializeField] private float startDelay = 0.3f;

    private RectTransform _rect;
    private Sequence _loop;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    private void OnEnable()
    {
        SetProgress(0f);

        _loop = DOTween.Sequence()
            .Append(DOVirtual.Float(0f, 1f, duration, SetProgress).SetEase(Ease.InOutSine))
            .AppendCallback(() => SetProgress(0f))
            .AppendInterval(interval)
            .SetDelay(startDelay)
            .SetLoops(-1)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        _loop?.Kill();
        _loop = null;

        SetProgress(0f);
    }

    private void SetProgress(float t)
    {
        var area = _rect.parent as RectTransform;
        float half = (area != null ? area.rect.width * 0.5f : 300f) + _rect.rect.width;

        _rect.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, t), _rect.anchoredPosition.y);
    }
}
