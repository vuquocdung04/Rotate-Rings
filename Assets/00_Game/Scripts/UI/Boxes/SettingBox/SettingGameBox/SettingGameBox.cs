using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingGameBox : SettingBaseBox
{
    public Button btnRestart;
    public Button btnHome;
    public Button btnCheat;

    [Header("Language")]
    [SerializeField] private Button btnLanguage;
    [SerializeField] private TMP_Text txtLanguage;

    [SerializeField] private GameObject musicTrackRow;

    protected override void OnInit()
    {
        btnRestart.onClick.AddListener(() => ShowQuit(QuitAction.Restart));

        if (btnHome != null)
            btnHome.onClick.AddListener(() => ShowQuit(QuitAction.Leave));

        if (btnCheat != null)
            btnCheat.onClick.AddListener(() => PopupManager.Show<CheatBox>().Forget());

        if (btnLanguage != null)
            btnLanguage.onClick.AddListener(NextLanguage);

        if (musicTrackRow != null) musicTrackRow.SetActive(false);
    }

    protected override void InitState()
    {
        base.InitState();
        RefreshLanguage();
    }

    private static void ShowQuit(QuitAction action) =>
        PopupManager.Show<QuitLevelBox>(box => box.Setup(action), cover: true).Forget();

    private void NextLanguage()
    {
        var languages = LocalizationManager.Languages;
        if (languages == null || languages.Count == 0) return;

        int index = 0;
        for (int i = 0; i < languages.Count; i++)
        {
            if (languages[i] == LocalizationManager.Language) index = i;
        }

        LocalizationManager.SetLanguage(languages[(index + 1) % languages.Count]);
        RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        if (txtLanguage != null) txtLanguage.text = LocalizationManager.Language;
    }
}
