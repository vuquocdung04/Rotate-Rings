using System;

public class UnlimitedHeartState : HeartState
{
    public override void Tick()
    {
        if (GetUnlimitedTimeRemaining().TotalSeconds > 0) return;

        UseProfile.IsUnlimitedHeart = false;
        UseProfile.TimeLastOverHeart = TimeManager.GetCurrentTime();

        Owner.SwitchToNormal();
        Owner.NotifyChanged();
    }

    public override bool TryUseHeart() => true;

    public override void AddUnlimited(double minutes)
    {
        UseProfile.TimeUnlimitedHeart = UseProfile.TimeUnlimitedHeart.AddMinutes(minutes);
        Owner.NotifyChanged();
    }

    public override double GetTimeToNextHeart() => 0;

    public override TimeSpan GetUnlimitedTimeRemaining()
    {
        TimeSpan remain = UseProfile.TimeUnlimitedHeart - TimeManager.GetCurrentTime();
        return remain.TotalSeconds > 0 ? remain : TimeSpan.Zero;
    }
}
