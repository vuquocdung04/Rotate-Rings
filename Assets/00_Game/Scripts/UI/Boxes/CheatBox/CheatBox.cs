using TMPro;
using UnityEngine.UI;

public class CheatBox : BaseBox, IPopupScale
{
    public Button btnNextLevel;
    public TMP_InputField inputNextLevel;

    public Button btnWin;
    public Button btnLose;
    public Button btnBuffCoin;
    public TMP_InputField inputBuffCoin;

    protected override void Init()
    {
    }

    protected override void InitState()
    {
    }
}
