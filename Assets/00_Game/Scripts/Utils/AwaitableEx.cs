using System;
using System.Collections.Generic;
using System.Threading;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Các tiện ích bù cho Awaitable "trần", thay thế những API mà UniTask có sẵn
/// nhưng UnityEngine.Awaitable thì không (Forget / WhenAll / WaitUntil / cầu nối DOTween...).
/// Đặt ở global namespace nên mọi nơi đều xài được, không cần using.
/// </summary>
public static class AwaitableEx
{
    // ===== Fire-and-forget: thay cho .Forget() của UniTask =====
    // Nuốt OperationCanceledException (object bị destroy giữa chừng là chuyện bình thường),
    // các exception khác thì log ra để không "chết lặng".
    public static async void Forget(this Awaitable awaitable)
    {
        try { await awaitable; }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static async void Forget<T>(this Awaitable<T> awaitable)
    {
        try { await awaitable; }
        catch (OperationCanceledException) { }
        catch (Exception e) { Debug.LogException(e); }
    }

    // ===== Awaitable đã hoàn tất: thay cho UniTask.CompletedTask =====
    public static Awaitable CompletedAwaitable()
    {
        var source = new AwaitableCompletionSource();
        source.SetResult();
        return source.Awaitable;
    }

    // ===== WhenAll: chờ tất cả hoàn tất (các Awaitable đã chạy song song từ trước) =====
    public static async Awaitable WhenAll(IEnumerable<Awaitable> awaitables)
    {
        foreach (var a in awaitables)
            await a;
    }

    public static async Awaitable WhenAll(params Awaitable[] awaitables)
    {
        foreach (var a in awaitables)
            await a;
    }

    // ===== WaitUntil: chờ tới khi predicate trả về true =====
    public static async Awaitable WaitUntil(Func<bool> predicate, CancellationToken token = default)
    {
        while (!predicate())
            await Awaitable.NextFrameAsync(token);
    }

    // ===== Delay theo thời gian thực, bỏ qua Time.timeScale =====
    // Thay cho UniTask.Delay(..., ignoreTimeScale: true). Cộng dồn unscaledDeltaTime
    // để đảm bảo đúng ngữ nghĩa dù game đang pause (timeScale = 0).
    public static async Awaitable WaitRealtimeAsync(float seconds, CancellationToken token = default)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            token.ThrowIfCancellationRequested();
            await Awaitable.NextFrameAsync(token);
            elapsed += Time.unscaledDeltaTime;
        }
    }

    // ===== Cầu nối DOTween: chờ tween chạy xong, thay cho .ToUniTask() =====
    // Khi bị cancel thì Kill tween rồi ném OperationCanceledException (giống hành vi mặc định của UniTask).
    public static async Awaitable ToAwaitable(this Tween tween, CancellationToken token = default)
    {
        try
        {
            while (tween != null && tween.IsActive() && !tween.IsComplete())
                await Awaitable.NextFrameAsync(token);
        }
        catch (OperationCanceledException)
        {
            if (tween != null && tween.IsActive()) tween.Kill();
            throw;
        }
    }
}
