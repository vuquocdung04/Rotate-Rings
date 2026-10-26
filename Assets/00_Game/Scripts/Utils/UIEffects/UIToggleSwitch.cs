using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIToggleSwitch : MonoBehaviour
{
    [SerializeField] private Graphic track;
    [SerializeField] private RectTransform knob;
    [SerializeField] private Color onColor = new Color(0.42f, 0.82f, 0.29f, 1f);
    [SerializeField] private Color offColor = new Color(0.79f, 0.73f, 0.68f, 1f);
    [SerializeField] private float knobOnX = 34f;
    [SerializeField] private float knobOffX = -34f;
    [SerializeField] private float duration = 0.22f;

    private bool _isOn;
    private bool _hasState;
    private Sequence _sequence;

    public bool IsOn => _isOn;

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;

        _hasState = false;
        Apply(_isOn);
    }

    public void SetOn(bool value)
    {
        bool animate = _hasState && isActiveAndEnabled && value != _isOn;

        _isOn = value;
        _hasState = true;

        _sequence?.Kill();
        _sequence = null;

        if (!animate)
        {
            Apply(value);
            return;
        }

        _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (knob != null)
        {
            _sequence.Insert(0f, knob.DOAnchorPosX(value ? knobOnX : knobOffX, duration).SetEase(Ease.OutBack, 1.4f));
            _sequence.Insert(0f, knob.DOScale(new Vector3(1.15f, 0.9f, 1f), duration * 0.4f).SetEase(Ease.OutQuad));
            _sequence.Insert(duration * 0.4f, knob.DOScale(Vector3.one, duration * 0.6f).SetEase(Ease.OutBack));
        }

        if (track != null)
            _sequence.Insert(0f, track.DOColor(value ? onColor : offColor, duration));
    }

    private void Apply(bool value)
    {
        if (knob != null)
        {
            knob.anchoredPosition = new Vector2(value ? knobOnX : knobOffX, knob.anchoredPosition.y);
            knob.localScale = Vector3.one;
        }

        if (track != null)
            track.color = value ? onColor : offColor;
    }
}
