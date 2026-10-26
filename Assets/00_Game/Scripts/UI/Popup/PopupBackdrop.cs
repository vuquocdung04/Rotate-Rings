using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PopupBackdrop : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private Image image;
    [SerializeField] private float fadeDuration = 0.2f;

    private Tween _tween;
    private bool _pressed;

    private void Reset() => image = GetComponent<Image>();

    private void OnDisable() => ReleasePress();

    public void OnPointerDown(PointerEventData eventData)
    {
        BaseBox top = PopupManager.Top;
        if (top == null) return;

        _pressed = true;
        top.OnBackdropPress();
    }

    public void OnPointerUp(PointerEventData eventData) => ReleasePress();

    public void OnPointerClick(PointerEventData eventData) => PopupManager.Top?.OnBackdropClick();

    private void ReleasePress()
    {
        if (!_pressed) return;
        _pressed = false;
        PopupManager.Top?.OnBackdropRelease();
    }

    public void ShowBackdrop(float alpha, bool fade)
    {
        image.raycastTarget = true;
        SetAlpha(alpha, fade);
    }

    public void HideBackdrop(bool fade)
    {
        image.raycastTarget = false;
        SetAlpha(0f, fade);
    }

    private void SetAlpha(float target, bool fade)
    {
        _tween?.Kill();

        if (fade && fadeDuration > 0f)
        {
            _tween = image.DOFade(target, fadeDuration).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        else
        {
            var c = image.color;
            c.a = target;
            image.color = c;
        }
    }
}
