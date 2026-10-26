using Dispatcher = EventDispatcher.EventDispatcher;

public static class RewardBindings
{
    public static void Bind(RewardManager reward)
    {
        if (reward == null) return;

        reward.Bind(RewardKeys.Coin, r =>
        {
            UseProfile.Coin += r.quantity;
            Dispatcher.Instance.PostEvent(EventID.CHANGE_COIN);
        });

        reward.Bind(RewardKeys.Heart, r => HeartManager.Instance.TryAddHeart(r.quantity));

        reward.Bind(RewardKeys.HeartUnlimited, r => HeartManager.Instance.TryAddUnlimited(r.quantity));
    }
}
