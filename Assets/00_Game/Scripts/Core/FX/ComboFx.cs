using System;
using UnityEngine;

[Serializable]
public class ComboFxTier
{
    public int combo = 3;
    public Color color = Color.white;
    public bool confetti = true;
    public bool full;
    public string sound = AudioKeys.SFX_REWARD_GIFTBOX;
    public bool vibrate = true;
    public long vibrateMilliseconds = 40;
}

public class ComboFx : MonoBehaviour
{
    [SerializeField] private ComboFxTier[] tiers =
    {
        new ComboFxTier { combo = 3, color = new Color(1f, 0.6f, 0.42f, 1f) },
        new ComboFxTier { combo = 4, color = new Color(0.71f, 0.48f, 1f, 1f) },
        new ComboFxTier { combo = 6, color = new Color(1f, 0.35f, 0.6f, 1f) },
        new ComboFxTier { combo = 10, color = new Color(1f, 0.82f, 0.2f, 1f), full = true, vibrateMilliseconds = 80 },
    };

    [Header("One Shot")]
    [SerializeField] private ParticleSystem[] glows;
    [SerializeField] private ParticleSystem[] sparks;
    [SerializeField] private ParticleSystem[] confetti;
    [SerializeField] private ParticleSystem[] celebrates;

    public void OnComboChanged(int previous, int current)
    {
        if (current <= 0 || current <= previous || tiers == null) return;

        ComboFxTier reached = null;

        for (int i = 0; i < tiers.Length; i++)
        {
            ComboFxTier tier = tiers[i];

            if (tier == null || tier.combo > current || tier.combo <= previous) continue;
            if (reached == null || tier.combo > reached.combo) reached = tier;
        }

        if (reached != null) Play(reached);
    }

    public void Play(int combo)
    {
        if (tiers == null) return;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i] != null && tiers[i].combo == combo)
            {
                Play(tiers[i]);
                return;
            }
        }
    }

    public void Play(ComboFxTier tier)
    {
        if (tier == null) return;

        if (!string.IsNullOrEmpty(tier.sound) && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(tier.sound);
        if (tier.vibrate) RDG.Vibration.Vibrate(tier.vibrateMilliseconds);

        SetStartColor(glows, tier.color);
        SetStartColor(sparks, tier.color);

        PlayBurst(glows);
        PlayBurst(sparks);

        if (tier.full) PlayBurst(celebrates);
        else if (tier.confetti) PlayBurst(confetti);
    }

    public void StopAll()
    {
        StopBurst(glows);
        StopBurst(sparks);
        StopBurst(confetti);
        StopBurst(celebrates);
    }

    private void OnDisable() => StopAll();

    private static void SetStartColor(ParticleSystem[] systems, Color color)
    {
        if (systems == null) return;

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;

            ParticleSystem.MainModule main = systems[i].main;
            main.startColor = color;
        }
    }

    private static void PlayBurst(ParticleSystem[] systems)
    {
        if (systems == null) return;

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;

            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            systems[i].Play(true);
        }
    }

    private static void StopBurst(ParticleSystem[] systems)
    {
        if (systems == null) return;

        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
