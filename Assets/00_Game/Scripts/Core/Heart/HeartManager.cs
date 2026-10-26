using System;
using System.Threading;
using EventDispatcher;
using UnityEngine;

public class HeartManager : MonoBehaviour
{
    public static HeartManager Instance { get; private set; }

    public const string FULL_LABEL = "FULL";

    public static bool BypassHeartCost { get; set; }

    [SerializeField] private HeartConfigSO config;

    public HeartConfigSO Config => config;

    public int MaxHearts => config != null ? config.maxHearts : 5;
    public double RefillSeconds => config != null ? config.RefillSeconds : 1800d;

    public int CurrentHeart => UseProfile.Heart;
    public bool IsUnlimited => UseProfile.IsUnlimitedHeart;
    public bool IsFull => CurrentHeart >= MaxHearts;

    public bool WasFirstPlay { get; private set; }
    public bool WasComebackReward { get; private set; }
    public double HoursSinceLastLogin { get; private set; }

    private readonly NormalHeartState _normalState = new();
    private readonly UnlimitedHeartState _unlimitedState = new();
    private HeartState _current;

    private CancellationTokenSource cts;
    private ToastManager toastManager;

    public void Init(ToastManager toastManager)
    {
        Instance = this;
        this.toastManager = toastManager;

        EnsureConfig();

        DateTime now = TimeManager.GetCurrentTime();

        WasFirstPlay = !GamePrefs.Has(StringHelper.HEART);
        WasComebackReward = false;
        HoursSinceLastLogin = WasFirstPlay ? 0d : (now - UseProfile.LastTimeLogin).TotalHours;

        if (WasFirstPlay)
        {
            UseProfile.Heart = config.startHearts;
            UseProfile.TimeLastOverHeart = now;
        }

        cts?.Dispose();
        cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        ChangeState(UseProfile.IsUnlimitedHeart ? (HeartState)_unlimitedState : _normalState);

        GrantSessionBonus();
        UseProfile.LastTimeLogin = now;

        TickLoop(cts.Token).Forget();
    }

    private void EnsureConfig()
    {
        if (config != null) return;

        config = Resources.Load<HeartConfigSO>(HeartConfigSO.RESOURCE_PATH);
        if (config != null) return;

        config = ScriptableObject.CreateInstance<HeartConfigSO>();
        Debug.LogWarning($"[HeartManager] Không tìm thấy Resources/{HeartConfigSO.RESOURCE_PATH}, dùng cấu hình mặc định.");
    }

    private void GrantSessionBonus()
    {
        if (WasFirstPlay)
        {
            if (config.grantUnlimitedOnFirstPlay && config.firstPlayUnlimitedMinutes > 0f)
                TryAddUnlimited(config.firstPlayUnlimitedMinutes);

            return;
        }

        if (!config.grantUnlimitedOnComeback) return;
        if (config.comebackThresholdHours <= 0f || config.comebackUnlimitedMinutes <= 0f) return;
        if (HoursSinceLastLogin < config.comebackThresholdHours) return;

        WasComebackReward = true;
        TryAddUnlimited(config.comebackUnlimitedMinutes);
    }

    private async Awaitable TickLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await AwaitableEx.WaitRealtimeAsync(1f, token);
                _current.Tick();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception e) { Debug.LogError($"Lỗi TickLoop: {e}"); }
        }
    }

    public void ChangeState(HeartState next)
    {
        if (_current == next) return;
        _current?.OnExit();
        _current = next;
        _current.OnEnter(this);
    }

    public void SwitchToNormal() => ChangeState(_normalState);
    public void SwitchToUnlimited() => ChangeState(_unlimitedState);

    public double GetTimeToNextHeart() => _current != null ? _current.GetTimeToNextHeart() : 0d;

    public double GetTimeToFull()
    {
        if (IsUnlimited || IsFull) return 0d;

        int missing = MaxHearts - CurrentHeart;
        return (missing - 1) * RefillSeconds + GetTimeToNextHeart();
    }

    public TimeSpan GetUnlimitedTimeRemaining() => _current != null ? _current.GetUnlimitedTimeRemaining() : TimeSpan.Zero;

    public void NotifyChanged()
    {
        if (!EventDispatcher.EventDispatcher.HasInstance()) return;
        if (!EventDispatcher.EventDispatcher.Instance.HasListener(EventID.CHANGE_HEART)) return;

        this.PostEvent(EventID.CHANGE_HEART);
    }

    // ========== PUBLIC API ==========

    public bool TryUseHeart()
    {
        if (BypassHeartCost) return true;

        return _current.TryUseHeart();
    }

    public bool CanAddHeart()
    {
        if (IsUnlimited)
        {
            toastManager.ShowToast("You have unlimited hearts");
            return false;
        }

        if (IsFull)
        {
            toastManager.ShowToast("Heart is full");
            return false;
        }

        return true;
    }

    public bool TryAddHeart(int amount = 1)
    {
        if (!CanAddHeart()) return false;

        SetHearts(CurrentHeart + amount);
        return true;
    }

    public void TryAddUnlimited(double minutes) => _current.AddUnlimited(minutes);

    public void TryShowHeartOffer()
    {
        if (!CanAddHeart()) return;

        PopupManager.Show<MoreLivesBox>().Forget();
    }

    // ========== MUTATION ==========

    public void SetHearts(int value)
    {
        int clamped = Mathf.Clamp(value, 0, MaxHearts);
        if (clamped == UseProfile.Heart) return;

        bool wasFull = IsFull;
        UseProfile.Heart = clamped;

        if (wasFull && clamped < MaxHearts)
            UseProfile.TimeLastOverHeart = TimeManager.GetCurrentTime();

        NotifyChanged();
    }

    public void Refill()
    {
        UseProfile.Heart = MaxHearts;
        UseProfile.TimeLastOverHeart = TimeManager.GetCurrentTime();

        NotifyChanged();
    }

    public void ClearUnlimited()
    {
        DateTime now = TimeManager.GetCurrentTime();

        UseProfile.IsUnlimitedHeart = false;
        UseProfile.TimeUnlimitedHeart = now.AddDays(-1);
        UseProfile.TimeLastOverHeart = now;

        SwitchToNormal();
        NotifyChanged();
    }

    public static void ClearSave()
    {
        GamePrefs.Delete(StringHelper.HEART);
        GamePrefs.Delete(StringHelper.IS_UNLIMITER_HEART);
        GamePrefs.Delete(StringHelper.TIME_UNLIMITER_HEART);
        GamePrefs.Delete(StringHelper.TIME_LAST_OVER_HEART);
        GamePrefs.Delete(StringHelper.LAST_TIME_LOGIN);
        GamePrefs.Flush();
    }

    // ========== INTERNAL ==========

    public void RefillOfflineHearts()
    {
        if (IsUnlimited) return;
        if (UseProfile.Heart >= MaxHearts) return;

        DateTime now = TimeManager.GetCurrentTime();
        TimeSpan timePassed = now - UseProfile.TimeLastOverHeart;

        int heartsGained = (int)(timePassed.TotalSeconds / RefillSeconds);
        if (heartsGained <= 0) return;

        int newCount = UseProfile.Heart + heartsGained;
        if (newCount >= MaxHearts)
        {
            UseProfile.Heart = MaxHearts;
            UseProfile.TimeLastOverHeart = now;
        }
        else
        {
            UseProfile.Heart = newCount;
            UseProfile.TimeLastOverHeart = UseProfile.TimeLastOverHeart
                .AddSeconds(heartsGained * RefillSeconds);
        }

        NotifyChanged();
    }

    public Cooldown HeartTimer()
    {
        if (IsUnlimited)
            return Cooldown.Until(UseProfile.TimeUnlimitedHeart);

        RefillOfflineHearts();
        return IsFull ? Cooldown.Done : Cooldown.InSeconds(GetTimeToNextHeart());
    }
}
