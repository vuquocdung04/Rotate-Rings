using UnityEngine;

[CreateAssetMenu(menuName = "Base/Heart/Heart Config", fileName = "HeartConfigSO")]
public class HeartConfigSO : ScriptableObject
{
    public const string RESOURCE_PATH = "HeartConfigSO";

    [Header("Core")]
    [Min(1)] public int maxHearts = 5;
    [Min(0.01f)] public float refillMinutes = 30f;
    [Min(0)] public int startHearts = 5;

    [Header("First Play")]
    public bool grantUnlimitedOnFirstPlay;
    [Min(0f)] public float firstPlayUnlimitedMinutes = 30f;

    [Header("Comeback")]
    public bool grantUnlimitedOnComeback;
    [Min(0f)] public float comebackThresholdHours = 24f;
    [Min(0f)] public float comebackUnlimitedMinutes = 30f;

    public double RefillSeconds => Mathf.Max(0.01f, refillMinutes) * 60.0;
}
