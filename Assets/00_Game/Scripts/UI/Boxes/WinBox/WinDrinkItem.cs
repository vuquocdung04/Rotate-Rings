using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WinDrinkItem : MonoBehaviour
{
    [SerializeField] private Image image;

    [Header("Spawn")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float startScale = 0.4f;
    [SerializeField] private float holdDuration;

    [Header("Fly")]
    [SerializeField] private float flyDuration = 0.6f;
    [SerializeField] private Vector2 hookOffset = new Vector2(0f, 200f);
    [SerializeField] private float hookRatio = 0.45f;
    [SerializeField] private Ease flyEase = Ease.InQuad;

    private Canvas canvas;
    private Sequence sequence;
    private Action<WinDrinkItem> onArrived;
    private Transform target;

    private float CanvasScale => canvas != null ? canvas.transform.localScale.x : 1f;

    public Transform Target => target;

    public float Duration => popDuration + holdDuration + flyDuration;

    public void SetSprite(Sprite sprite)
    {
        if (image == null) image = GetComponent<Image>();
        if (image != null && sprite != null) image.sprite = sprite;
    }

    public void SetSpawn(Transform start, float radius)
    {
        CacheCanvas();

        Vector2 offset = UnityEngine.Random.insideUnitCircle * radius;
        transform.localScale = Vector3.one * startScale;

        if (start == null)
        {
            transform.localPosition = offset;
            return;
        }

        transform.position = start.position + (Vector3)(offset * CanvasScale);
    }

    public void FlyTo(Transform target, float delay, Action<WinDrinkItem> onArrived)
    {
        this.onArrived = onArrived;
        this.target = target;

        CacheCanvas();
        Kill();

        if (target == null)
        {
            Arrived();
            return;
        }

        Vector3 start = transform.position;
        Vector3 end = target.position;
        Vector3 hook = Vector3.Lerp(start, end, hookRatio) + (Vector3)(hookOffset * CanvasScale);
        Vector3[] path = { start, hook, end };

        sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);

        if (delay > 0f) sequence.AppendInterval(delay);

        sequence.Append(transform.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack));

        if (holdDuration > 0f) sequence.AppendInterval(holdDuration);

        sequence.Append(transform.DOPath(path, flyDuration, PathType.CatmullRom).SetEase(flyEase));
        sequence.OnComplete(Arrived);
    }

    public void Stop()
    {
        onArrived = null;
        Kill();
    }

    private void CacheCanvas()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
    }

    private void Arrived()
    {
        Action<WinDrinkItem> callback = onArrived;
        onArrived = null;
        callback?.Invoke(this);
    }

    private void Kill()
    {
        sequence?.Kill();
        sequence = null;
    }

    private void OnDestroy()
    {
        onArrived = null;
        Kill();
    }
}
