using System.Collections.Generic;
using DG.Tweening;
using RDG;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private static readonly List<Graphic> GraphicBuffer = new();

    [SerializeField] private float pressScale = 0.8f;
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.15f;
    [SerializeField] private Ease releaseEase = Ease.OutBack;

    [Header("Feedback")]
    [SerializeField] private string soundKey = AudioKeys.CLICK;
    [SerializeField] private bool useVibrate = true;
    [SerializeField] private long vibrateMilliseconds = 50;

    private readonly List<Graphic> _raycastGraphics = new();
    private readonly List<Vector4> _defaultPaddings = new();

    private RectTransform _rect;
    private Button _button;
    private Vector3 _originalScale;
    private Tween _tween;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _button = GetComponent<Button>();
        _originalScale = _rect.localScale;

        CacheRaycastGraphics();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.IsInteractable())
            return;

        PlayFeedback();
        ExpandRaycastArea();

        _tween?.Kill();
        _tween = _rect.DOScale(_originalScale * pressScale, pressDuration).SetEase(Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _tween?.Kill();
        _tween = _rect.DOScale(_originalScale, releaseDuration)
            .SetEase(releaseEase)
            .OnComplete(RestoreRaycastArea);
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _rect.localScale = _originalScale;

        RestoreRaycastArea();
    }

    private void PlayFeedback()
    {
        if (!string.IsNullOrEmpty(soundKey) && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(soundKey);

        if (useVibrate)
            Vibration.Vibrate(vibrateMilliseconds);
    }

    private void CacheRaycastGraphics()
    {
        _raycastGraphics.Clear();
        _defaultPaddings.Clear();

        GetComponentsInChildren(true, GraphicBuffer);

        for (int i = 0; i < GraphicBuffer.Count; i++)
        {
            Graphic graphic = GraphicBuffer[i];

            if (!graphic.raycastTarget)
                continue;

            _raycastGraphics.Add(graphic);
            _defaultPaddings.Add(graphic.raycastPadding);
        }

        GraphicBuffer.Clear();
    }

    private void ExpandRaycastArea()
    {
        float factor = 1f / Mathf.Max(pressScale, 0.01f) - 1f;

        if (factor <= 0f)
            return;

        for (int i = 0; i < _raycastGraphics.Count; i++)
        {
            Graphic graphic = _raycastGraphics[i];

            if (graphic == null)
                continue;

            Rect rect = graphic.rectTransform.rect;
            Vector3 offset = _rect.InverseTransformPoint(graphic.rectTransform.position);
            Vector4 padding = _defaultPaddings[i];

            graphic.raycastPadding = new Vector4(
                padding.x + (rect.xMin + offset.x) * factor,
                padding.y + (rect.yMin + offset.y) * factor,
                padding.z - (rect.xMax + offset.x) * factor,
                padding.w - (rect.yMax + offset.y) * factor);
        }
    }

    private void RestoreRaycastArea()
    {
        for (int i = 0; i < _raycastGraphics.Count; i++)
        {
            Graphic graphic = _raycastGraphics[i];

            if (graphic == null)
                continue;

            graphic.raycastPadding = _defaultPaddings[i];
        }
    }
}
