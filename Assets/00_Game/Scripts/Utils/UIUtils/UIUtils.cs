using System;
using System.Threading;
using EventDispatcher;
using TMPro;
using UnityEngine;

public readonly struct Cooldown
{
    public readonly DateTime EndUtc;
    private Cooldown(DateTime end) => EndUtc = end;

    public static Cooldown Until(DateTime end) => new(end);
    public static Cooldown InSeconds(double s) => new(TimeManager.GetCurrentTime().AddSeconds(s));
    public static readonly Cooldown Done = new(DateTime.MinValue);

    public TimeSpan Remaining
    {
        get
        {
            var r = EndUtc - TimeManager.GetCurrentTime();
            return r > TimeSpan.Zero ? r : TimeSpan.Zero;
        }
    }

    public bool IsDone => Remaining <= TimeSpan.Zero;
}

public static partial class UIUtils
{
    private static string DefaultFormat(TimeSpan t) => $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
    public static void BindCooldown(
     this TMP_Text text, MonoBehaviour owner,
     Func<Cooldown> source, string doneText = "",
     Func<TimeSpan, string> format = null, Action onReady = null,
     EventID? refreshOn = null)
    {
        format ??= DefaultFormat;
        CooldownLoop(text, owner, source, doneText, format, onReady, refreshOn,
            owner.destroyCancellationToken).Forget();
    }

    private static async Awaitable CooldownLoop(
        TMP_Text text, MonoBehaviour owner, Func<Cooldown> source, string doneText,
        Func<TimeSpan, string> format, Action onReady, EventID? refreshOn, CancellationToken token)
    {
        int lastKey = int.MinValue;
        bool doneFired = false;

        try
        {
            while (!token.IsCancellationRequested)
            {
                Cooldown cd = source();
                TimeSpan remain = cd.Remaining;

                if (remain <= TimeSpan.Zero)
                {
                    if (lastKey != -1) { lastKey = -1; text.text = doneText; }
                    if (!doneFired) { doneFired = true; onReady?.Invoke(); }
                }
                else
                {
                    doneFired = false;
                    int key = (int)remain.TotalSeconds;
                    if (key != lastKey) { lastKey = key; text.text = format(remain); }
                }

                double frac = remain.TotalSeconds - Math.Floor(remain.TotalSeconds);
                double wait = (remain <= TimeSpan.Zero || frac < 0.001) ? 1.0 : frac;

                using var wakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                void Wake(object _) => wakeCts.Cancel();
                if (refreshOn.HasValue) owner.RegisterListener(refreshOn.Value, Wake);

                try
                {
                    await AwaitableEx.WaitRealtimeAsync((float)wait, wakeCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested) return;

                }
                finally
                {
                    if (refreshOn.HasValue) owner.RemoveListener(refreshOn.Value, Wake);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public static void BindHeartTimer(this TMP_Text text, MonoBehaviour owner)
    {
        text.BindCooldown(owner,
            source: () =>
            {
                var hm = HeartManager.Instance;
                return hm != null ? hm.HeartTimer() : Cooldown.Done;
            },
            doneText: HeartManager.FULL_LABEL,
            format: TimeManager.Format,
            refreshOn: EventID.CHANGE_HEART);
    }

    // ============= COUNT UP/DOWN NUMBER =============
    public static async Awaitable CountTo(
      this TMP_Text text, double target, float duration = 0.5f,
      double? from = null, Func<double, string> format = null,
      CancellationToken token = default, string prefix = "", string suffix = "")
    {
        format ??= NumberFormatter.Format;
        bool hasAffix = prefix.Length > 0 || suffix.Length > 0;
        double start = from ?? (!hasAffix && double.TryParse(text.text, out double parsed) ? parsed : 0);

        if (duration <= 0f || Math.Abs(start - target) < 0.5)
        {
            text.text = prefix + format(target) + suffix;
            return;
        }

        float elapsed = 0f;
        string lastShown = null;

        try
        {
            while (elapsed < duration && !token.IsCancellationRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                double current = start + (target - start) * (elapsed / duration);
                string shown = prefix + format(current) + suffix;
                if (shown != lastShown)
                {
                    lastShown = shown;
                    text.text = shown;
                }
                await Awaitable.NextFrameAsync(token);
            }
        }
        catch (OperationCanceledException) { return; }

        if (!token.IsCancellationRequested) text.text = prefix + format(target) + suffix;
    }
    // ============= CANVAS =============
    public static void SetCanvasState(this CanvasGroup cg, bool isInteractable, float alpha = -1f)
    {
        if (cg == null) return;
        cg.interactable = isInteractable;
        cg.blocksRaycasts = isInteractable;
        if (alpha >= 0) cg.alpha = alpha;
    }
}