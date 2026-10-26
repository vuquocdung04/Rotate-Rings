using DG.Tweening;
using UnityEngine;

public class UIWobble : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField] private float pulseScale = 1.06f;
    [SerializeField] private float pulseDuration = 0.6f;

    [Header("Wobble")]
    [SerializeField] private float startDelay = 1f;
    [SerializeField] private float interval = 3f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float angle = 12f;
    [SerializeField] private float punch = 0.12f;
    [SerializeField] private int vibrato = 8;
    [SerializeField] private float elasticity = 0.8f;

    private Vector3 _originalScale;
    private Quaternion _originalRotation;
    private Tween _pulse;
    private Sequence _wobble;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _originalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        _pulse = transform.DOScale(_originalScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);

        _wobble = DOTween.Sequence()
            .AppendCallback(BeginWobble)
            .Append(transform.DOPunchRotation(new Vector3(0f, 0f, angle), duration, vibrato, elasticity))
            .Join(transform.DOPunchScale(_originalScale * punch, duration, vibrato, elasticity))
            .AppendCallback(EndWobble)
            .AppendInterval(interval)
            .SetLoops(-1)
            .SetDelay(startDelay)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        _pulse?.Kill();
        _wobble?.Kill();
        _pulse = null;
        _wobble = null;

        ResetPose();
    }

    private void BeginWobble()
    {
        _pulse?.Pause();
        ResetPose();
    }

    private void EndWobble()
    {
        ResetPose();
        _pulse?.Restart();
    }

    private void ResetPose()
    {
        transform.localScale = _originalScale;
        transform.localRotation = _originalRotation;
    }
}
