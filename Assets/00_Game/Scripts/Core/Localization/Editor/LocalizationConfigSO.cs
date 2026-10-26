using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Base/Localization/Localization Config", fileName = "LocalizationConfigSO")]
public class LocalizationConfigSO : ScriptableObject
{
    [Header("Google Sheet")]
    [Tooltip("Link sheet hoặc chỉ Spreadsheet Id. Sheet phải share 'Anyone with the link → Viewer'.")]
    public string spreadsheetUrl;

    [Tooltip("Mỗi tab một dòng. Gid lấy ở đuôi link tab: ...#gid=123456. Tab đầu tiên gid = 0.")]
    public List<LocalizationSheetEntry> sheets = new() { new LocalizationSheetEntry { name = "Main", gid = 0 } };

    [Header("Output")]
    [Tooltip("Tên cột ngôn ngữ dùng khi máy không khớp ngôn ngữ nào và khi thiếu bản dịch.")]
    public string fallbackLanguage = "English";
}

[Serializable]
public class LocalizationSheetEntry
{
    public string name;
    public long gid;
}
