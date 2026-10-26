using DG.Tweening;
using UnityEngine;

public class UIAttentionEffect : MonoBehaviour
{
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
        const float pulseScale = 1.06f;
        const float pulseDuration = 0.6f;
        const float wobbleDelay = 1f;
        const float wobbleInterval = 3f;
        const float wobbleDuration = 0.5f;
        const float wobbleAngle = 12f;
        const float wobblePunch = 0.12f;
        const int vibrato = 8;
        const float elasticity = 0.8f;

        _pulse = transform.DOScale(_originalScale * pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);

        _wobble = DOTween.Sequence()
            .AppendCallback(BeginWobble)
            .Append(transform.DOPunchRotation(new Vector3(0f, 0f, wobbleAngle), wobbleDuration, vibrato, elasticity))
            .Join(transform.DOPunchScale(_originalScale * wobblePunch, wobbleDuration, vibrato, elasticity))
            .AppendCallback(EndWobble)
            .AppendInterval(wobbleInterval)
            .SetLoops(-1)
            .SetDelay(wobbleDelay)
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
