using DG.Tweening;
using UnityEngine;

public class UIRotateLoop : MonoBehaviour
{
    [SerializeField] private float duration = 3f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private bool ignoreTimeScale = true;

    private Quaternion _originalRotation;
    private Tween _tween;

    private void Awake()
    {
        _originalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        Stop();
        transform.localRotation = _originalRotation;
    }

    public void Play()
    {
        _tween?.Kill();

        float angle = clockwise ? -360f : 360f;
        _tween = transform
            .DOLocalRotate(new Vector3(0f, 0f, angle), Mathf.Max(0.01f, duration), RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
    }

    public void Stop()
    {
        _tween?.Kill();
        _tween = null;
    }

    public void SetDuration(float value)
    {
        duration = value;

        if (_tween != null)
            Play();
    }
}
