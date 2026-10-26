using DG.Tweening;
using TMPro;
using UnityEngine;

public class SplashView : MonoBehaviour
{
    private const int MaxDot = 3;

    [Header("Logo")]
    [SerializeField] private RectTransform icon;
    [SerializeField] private float iconPopDuration = 0.5f;
    [SerializeField] private float bobHeight = 20f;
    [SerializeField] private float bobDuration = 0.6f;

    [Header("Slogan")]
    [SerializeField] private CanvasGroup slogan;
    [SerializeField] private float sloganDelay = 0.1f;
    [SerializeField] private float sloganDuration = 0.4f;
    [SerializeField] private float sloganRise = 24f;

    [Header("Loading Text")]
    [SerializeField] private TMP_Text txtLoading;
    [SerializeField] private float dotInterval = 0.35f;

    private string label;
    private int dotCount;

    private void Start()
    {
        PlayLoadingText();
        PlayIcon();
    }

    private void OnDestroy() => DOTween.Kill(this);

    private void PlayLoadingText()
    {
        if (txtLoading == null) return;

        label = LocalizationManager.HasKey(LocalizationKeys.UI_LOADING)
            ? LocalizationManager.Get(LocalizationKeys.UI_LOADING).TrimEnd('.')
            : txtLoading.text.TrimEnd('.');
        dotCount = 0;
        txtLoading.text = label;

        DOVirtual.DelayedCall(dotInterval, UpdateDot).SetLoops(-1).SetTarget(this).SetUpdate(true);
    }

    private void UpdateDot()
    {
        dotCount = (dotCount + 1) % (MaxDot + 1);
        txtLoading.text = label + new string('.', dotCount);
    }

    private void PlayIcon()
    {
        if (slogan != null) slogan.alpha = icon != null ? 0f : 1f;
        if (icon == null) return;

        icon.localScale = Vector3.zero;
        icon.DOScale(1f, iconPopDuration)
            .SetEase(Ease.OutBack)
            .SetTarget(this)
            .SetUpdate(true)
            .OnComplete(OnIconPopped);
    }

    private void OnIconPopped()
    {
        icon.DOAnchorPosY(icon.anchoredPosition.y + bobHeight, bobDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this)
            .SetUpdate(true);

        PlaySlogan();
    }

    private void PlaySlogan()
    {
        if (slogan == null) return;

        var rect = (RectTransform)slogan.transform;
        Vector2 rest = rect.anchoredPosition;
        rect.anchoredPosition = rest - new Vector2(0f, sloganRise);

        DOTween.Sequence()
            .AppendInterval(sloganDelay)
            .Append(slogan.DOFade(1f, sloganDuration).SetEase(Ease.OutQuad))
            .Join(rect.DOAnchorPos(rest, sloganDuration).SetEase(Ease.OutBack))
            .SetTarget(this)
            .SetUpdate(true);
    }
}
