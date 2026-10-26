using System;

public class NormalHeartState : HeartState
{
    public override void OnEnter(HeartManager owner)
    {
        base.OnEnter(owner);
        Owner.RefillOfflineHearts();
    }

    public override void Tick()
    {
        Owner.RefillOfflineHearts();
    }

    public override bool TryUseHeart()
    {
        Owner.RefillOfflineHearts();

        if (UseProfile.Heart <= 0) return false;

        Owner.SetHearts(UseProfile.Heart - 1);
        return true;
    }

    public override void AddUnlimited(double minutes)
    {
        UseProfile.IsUnlimitedHeart = true;
        UseProfile.TimeUnlimitedHeart = TimeManager.GetCurrentTime().AddMinutes(minutes);

        Owner.SwitchToUnlimited();
        Owner.NotifyChanged();
    }

    public override double GetTimeToNextHeart()
    {
        if (UseProfile.Heart >= Owner.MaxHearts) return 0;

        TimeSpan elapsed = TimeManager.GetCurrentTime() - UseProfile.TimeLastOverHeart;
        return Math.Max(0, Owner.RefillSeconds - elapsed.TotalSeconds);
    }

    public override TimeSpan GetUnlimitedTimeRemaining() => TimeSpan.Zero;
}
