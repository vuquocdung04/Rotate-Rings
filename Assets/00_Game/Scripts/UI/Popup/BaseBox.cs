using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using EventDispatcher;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(GraphicRaycaster))]
public abstract class BaseBox : MonoBehaviour
{
    [SerializeField] protected RectTransform mainPanel;
    [SerializeField] protected CanvasGroup canvasGroup;
    [SerializeField] protected List<Button> closeButtons = new();

    private PopupEntry _setting;
    private CancellationTokenSource _animCts;
    private Tween _peekTween;
    private float _peekFactor = 1f;
    private bool _postedOpen;
    private bool _bootstrapped;

    private PopupEntry Setting => _setting ??= PopupManager.SettingFor(GetType().Name);

    internal float BackdropAlpha => Setting.backdropAlpha * _peekFactor;

    protected virtual void Init() { }
    protected virtual void InitState() { }

    protected virtual void Awake() => Bootstrap();

    internal void Bootstrap()
    {
        if (_bootstrapped) return;
        _bootstrapped = true;

        canvasGroup.SetCanvasState(false, 0f);

        for (int i = 0; i < closeButtons.Count; i++)
        {
            if (closeButtons[i] != null) closeButtons[i].onClick.AddListener(Close);
        }

        Init();
    }

    protected virtual void OnDestroy()
    {
        PopupManager.Unregister(this);
        CancelAnim();
        KillPeekTween();
    }

    // ========== SHOW / CLOSE ==========
    public void Show(bool cover = false)
    {
        ShowInternal(null, false, false);
        PopupManager.Push(this, cover);
    }

    public void ShowDetached(bool instant = false) => ShowInternal(null, true, instant);

    public void ShowDetached(IPopupAnim anim) => ShowInternal(anim, true, false);

    public void Close() => CloseInternal(null, true);

    public void CloseDetached(IPopupAnim anim = null) => CloseInternal(anim, false);

    private void ShowInternal(IPopupAnim anim, bool detached, bool instant)
    {
        InitState();
        CancelPeek();

        PopupManager.Attach(transform, detached);
        transform.SetAsLastSibling();

        if (!_postedOpen)
        {
            _postedOpen = true;
            PostEventSafe(EventID.POPUP_OPENED);
        }

        RunShow(anim, instant).Forget();
    }

    private void CloseInternal(IPopupAnim anim, bool notifyStack)
    {
        if (_postedOpen)
        {
            _postedOpen = false;
            PostEventSafe(EventID.POPUP_CLOSED);
        }

        CancelPeek();
        RunClose(anim, notifyStack).Forget();
    }

    private async Awaitable RunShow(IPopupAnim anim, bool instant)
    {
        await PlayShow(anim, instant, RestartAnim());
    }

    private async Awaitable RunClose(IPopupAnim anim, bool notifyStack)
    {
        CancellationToken token = RestartAnim();

        try { await PlayHide(anim, token); }
        catch (OperationCanceledException) { return; }

        if (token.IsCancellationRequested) return;
        FinishClose(notifyStack);
    }

    private Awaitable PlayShow(IPopupAnim anim, bool instant, CancellationToken token)
    {
        if (instant) return PopupAnims.Instant(mainPanel, canvasGroup, true);

        float duration = Setting.animDuration;

        if (anim != null)
            return anim.Show(mainPanel, canvasGroup, duration, token);

        if (this is IPopupScale)
            return PopupAnims.ScaleShow(mainPanel, canvasGroup, duration, token);

        return PopupAnims.Instant(mainPanel, canvasGroup, true);
    }

    private Awaitable PlayHide(IPopupAnim anim, CancellationToken token)
    {
        float duration = Setting.animDuration;

        if (anim != null)
            return anim.Hide(mainPanel, canvasGroup, duration, token);

        if (this is IPopupScale)
            return PopupAnims.ScaleHide(mainPanel, canvasGroup, duration, token);

        return PopupAnims.Instant(mainPanel, canvasGroup, false);
    }

    private void FinishClose(bool notifyStack)
    {
        canvasGroup.SetCanvasState(false, 0f);

        if (notifyStack)
        {
            BaseBox toResume = PopupManager.Pop(this);
            if (toResume != null) toResume.Resume();
            PopupManager.RefreshBackdrop(PopupManager.IsEmpty);
        }
    }

    // ========== STACK ==========
    internal void Suspend()
    {
        CancelAnim();
        CancelPeek();
        canvasGroup.SetCanvasState(false, 0f);
    }

    internal void Resume()
    {
        transform.SetAsLastSibling();
        RunShow(null, false).Forget();
    }

    // ========== BACKDROP INPUT ==========
    internal void OnBackdropPress()
    {
        if (Setting.peekOnBackdropHold) SetPeek(true);
    }

    internal void OnBackdropRelease()
    {
        if (Setting.peekOnBackdropHold) SetPeek(false);
    }

    internal void OnBackdropClick()
    {
        if (Setting.peekOnBackdropHold || !Setting.closeOnBackdropClick) return;
        Close();
    }

    // ========== PEEK ==========
    private void SetPeek(bool on)
    {
        KillPeekTween();
        _peekFactor = on ? 0f : 1f;
        _peekTween = canvasGroup.DOFade(_peekFactor, Setting.peekFadeDuration).SetUpdate(true);

        PopupManager.RefreshBackdrop(true);
    }

    private void CancelPeek()
    {
        KillPeekTween();
        if (_peekFactor >= 1f) return;

        _peekFactor = 1f;
        canvasGroup.alpha = 1f;
    }

    private void KillPeekTween()
    {
        _peekTween?.Kill();
        _peekTween = null;
    }

    // ========== HELPERS ==========
    private CancellationToken RestartAnim()
    {
        CancelAnim();
        _animCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        return _animCts.Token;
    }

    private void CancelAnim()
    {
        if (_animCts == null) return;

        _animCts.Cancel();
        _animCts.Dispose();
        _animCts = null;
    }

    protected void PostEventSafe(EventID id)
    {
        if (!EventDispatcher.EventDispatcher.HasInstance()) return;
        if (!EventDispatcher.EventDispatcher.Instance.HasListener(id)) return;

        this.PostEvent(id);
    }

}
