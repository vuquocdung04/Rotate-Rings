public static class NumberFormatter
{
    public static string Format(double value)
    {
        double abs = System.Math.Abs(value);
        if (abs < 10000) return ((long)value).ToString();
        if (abs < 1_000_000) return $"{value / 1_000.0:0.0}K";
        if (abs < 1_000_000_000) return $"{value / 1_000_000.0:0.0}M";
        if (abs < 1_000_000_000_000) return $"{value / 1_000_000_000.0:0.0}B";
        return $"{value / 1_000_000_000_000.0:0.0}T";
    }
}
