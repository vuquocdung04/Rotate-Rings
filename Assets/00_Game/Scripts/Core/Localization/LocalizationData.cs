using System;
using System.Collections.Generic;

[Serializable]
public class LocalizationData
{
    public string fallback = "English";
    public List<string> languages = new();
    public Dictionary<string, Dictionary<string, string>> texts = new();
}
