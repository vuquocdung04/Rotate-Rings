using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class SettingControl
{
    public Button btn;
    public GameObject objOn;
    public GameObject objOff;
    public UIToggleSwitch toggle;

    private Func<bool> get;
    private Action<bool> set;

    public void Setup(Func<bool> get, Action<bool> set)
    {
        this.get = get;
        this.set = set;

        btn.onClick.AddListener(() => Apply(!get()));

        Refresh();
    }

    public void Refresh()
    {
        if (get == null) return;

        bool isOn = get();
        if (objOn != null) objOn.SetActive(isOn);
        if (objOff != null) objOff.SetActive(!isOn);
        if (toggle != null) toggle.SetOn(isOn);
    }

    private void Apply(bool value)
    {
        set(value);
        Refresh();
    }
}

public abstract class SettingBaseBox : BaseBox, IPopupScale
{
    [Header("Setting Controls")]
    [SerializeField] protected SettingControl sound;
    [SerializeField] protected SettingControl music;
    [SerializeField] protected SettingControl vib;

    public SettingControl Sound => sound;
    public SettingControl Music => music;
    public SettingControl Vib => vib;

    protected sealed override void Init()
    {
        sound.Setup(() => UseProfile.OnSound, value => AudioManager.Instance.SetSound(value));
        music.Setup(() => UseProfile.OnMusic, value => AudioManager.Instance.SetMusic(value));
        vib.Setup(() => UseProfile.OnVib, value => UseProfile.OnVib = value);

        OnInit();
    }

    protected override void InitState()
    {
        sound.Refresh();
        music.Refresh();
        vib.Refresh();
    }

    protected virtual void OnInit() { }
}
