using System;

public static class UseProfile
{
    public static int Level { get => GamePrefs.Get(StringHelper.LEVEL, 1); set => GamePrefs.Set(StringHelper.LEVEL, value); }
    public static int Coin { get => GamePrefs.Get(StringHelper.COIN, 0); set => GamePrefs.Set(StringHelper.COIN, value); }

    // --- SETTINGS ---
    public static bool OnMusic { get => GamePrefs.Get(StringHelper.ONOFF_MUSIC, true); set => GamePrefs.Set(StringHelper.ONOFF_MUSIC, value); }
    public static bool OnSound { get => GamePrefs.Get(StringHelper.ONOFF_SOUND, true); set => GamePrefs.Set(StringHelper.ONOFF_SOUND, value); }
    public static bool OnVib { get => GamePrefs.Get(StringHelper.ONOFF_VIB, true); set => GamePrefs.Set(StringHelper.ONOFF_VIB, value); }

    // --- TIM & QUẢNG CÁO ---
    public static int Heart { get => GamePrefs.Get(StringHelper.HEART, 5); set => GamePrefs.Set(StringHelper.HEART, value); }
    public static bool IsUnlimitedHeart { get => GamePrefs.Get(StringHelper.IS_UNLIMITER_HEART, false); set => GamePrefs.Set(StringHelper.IS_UNLIMITER_HEART, value); }

    public static DateTime TimeUnlimitedHeart
    {
        get => DateTime.FromBinary(GamePrefs.Get(StringHelper.TIME_UNLIMITER_HEART, TimeManager.GetCurrentTime().AddDays(-1).ToBinary()));
        set => GamePrefs.Set(StringHelper.TIME_UNLIMITER_HEART, value.ToBinary());
    }

    public static DateTime TimeLastOverHeart
    {
        get => DateTime.FromBinary(GamePrefs.Get(StringHelper.TIME_LAST_OVER_HEART, TimeManager.GetCurrentTime().ToBinary()));
        set => GamePrefs.Set(StringHelper.TIME_LAST_OVER_HEART, value.ToBinary());
    }

    public static DateTime LastTimeLogin
    {
        get => DateTime.FromBinary(GamePrefs.Get(StringHelper.LAST_TIME_LOGIN, TimeManager.GetCurrentTime().ToBinary()));
        set => GamePrefs.Set(StringHelper.LAST_TIME_LOGIN, value.ToBinary());
    }
}
