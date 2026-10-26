using System.Threading;
using DG.Tweening;
using UnityEngine;

public interface IPopupScale { }

public interface IPopupAnim
{
    Awaitable Show(RectTransform panel, CanvasGroup group, float duration, CancellationToken token);
    Awaitable Hide(RectTransform panel, CanvasGroup group, float duration, CancellationToken token);
}

public static class PopupAnims
{
    private const Ease ScaleShowEase = Ease.OutBack;
    private const Ease ScaleHideEase = Ease.InBack;
    private const float HideRatio = 0.8f;
    private const float ClosedScale = 0.8f;

    public static Awaitable ScaleShow(RectTransform panel, CanvasGroup group, float duration, CancellationToken token)
    {
        panel.DOKill();
        group.DOKill();
        panel.localScale = Vector3.one * ClosedScale;
        group.SetCanvasState(true, 0f);

        return DOTween.Sequence()
            .Join(panel.DOScale(Vector3.one, duration).SetEase(ScaleShowEase))
            .Join(group.DOFade(1f, duration))
            .SetUpdate(true)
            .ToAwaitable(token);
    }

    public static Awaitable ScaleHide(RectTransform panel, CanvasGroup group, float duration, CancellationToken token)
    {
        panel.DOKill();
        group.DOKill();
        group.SetCanvasState(false);

        return DOTween.Sequence()
            .Join(panel.DOScale(Vector3.one * ClosedScale, duration * HideRatio).SetEase(ScaleHideEase))
            .Join(group.DOFade(0f, duration * HideRatio))
            .SetUpdate(true)
            .ToAwaitable(token);
    }

    public static Awaitable Instant(RectTransform panel, CanvasGroup group, bool visible)
    {
        panel.DOKill();
        group.DOKill();

        if (visible)
        {
            panel.localScale = Vector3.one;
            group.SetCanvasState(true, 1f);
        }
        else
        {
            group.SetCanvasState(false, 0f);
        }

        return AwaitableEx.CompletedAwaitable();
    }
}
