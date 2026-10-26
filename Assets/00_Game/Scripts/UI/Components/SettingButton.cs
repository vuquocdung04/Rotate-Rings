using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SettingButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => PopupManager.Show<SettingGameBox>().Forget());
    }
}
