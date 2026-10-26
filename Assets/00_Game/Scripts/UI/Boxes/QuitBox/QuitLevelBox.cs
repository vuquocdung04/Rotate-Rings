using UnityEngine.UI;

public enum QuitAction
{
    Leave,
    Restart,
}

public class QuitLevelBox : BaseBox, IPopupScale
{
    public Button btnLeave;
    public Button btnRestart;

    private QuitAction action = QuitAction.Restart;
    private UIPopupFeel feel;

    public void Setup(QuitAction quitAction) => action = quitAction;

    protected override void Init() => feel = GetComponent<UIPopupFeel>();

    protected override void InitState()
    {
        if (btnLeave != null) btnLeave.gameObject.SetActive(action == QuitAction.Leave);
        if (btnRestart != null) btnRestart.gameObject.SetActive(action == QuitAction.Restart);
        if (feel != null) feel.Play();
    }
}
