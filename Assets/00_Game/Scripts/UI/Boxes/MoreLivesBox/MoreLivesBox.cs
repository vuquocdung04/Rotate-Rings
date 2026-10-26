using EventDispatcher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoreLivesBox : BaseBox, IPopupScale
{
    public Button btnRefill;
    public Button btnRefillFree;
    public TextMeshProUGUI txtCost;
    public TextMeshProUGUI txtHeart;
    public TextMeshProUGUI txtTimer;
    [Min(0)] public int cost = 900;
    [Min(1)] public int freeHearts = 1;

    protected override void Init()
    {
        if (btnRefill != null) btnRefill.onClick.AddListener(OnRefill);
        if (btnRefillFree != null) btnRefillFree.onClick.AddListener(OnRefillFree);
        if (txtTimer != null) txtTimer.BindHeartTimer(this);
    }

    protected override void InitState()
    {
        if (txtCost != null) txtCost.text = NumberFormatter.Format(cost);
        RefreshHeart();
    }

    private void OnEnable() => this.RegisterListener(EventID.CHANGE_HEART, OnHeartChanged);

    private void OnDisable() => this.RemoveListener(EventID.CHANGE_HEART, OnHeartChanged);

    private void OnHeartChanged(object _) => RefreshHeart();

    private void RefreshHeart()
    {
        HeartManager hm = HeartManager.Instance;
        if (txtHeart != null && hm != null) txtHeart.text = hm.CurrentHeart.ToString();
    }

    private void OnRefill()
    {
        HeartManager hm = HeartManager.Instance;
        if (hm == null || !hm.CanAddHeart()) return;

        if (UseProfile.Coin < cost)
        {
            if (GameManager.Instance != null) GameManager.Instance.toastManager.ShowToast("Not enough coins");
            return;
        }

        UseProfile.Coin -= cost;
        PostEventSafe(EventID.CHANGE_COIN);
        Claim(hm.MaxHearts - hm.CurrentHeart);
    }

    private void OnRefillFree()
    {
        HeartManager hm = HeartManager.Instance;
        if (hm == null || !hm.CanAddHeart()) return;

        Claim(freeHearts);
    }

    private void Claim(int hearts)
    {
        Close();

        if (RewardManager.Instance != null)
            RewardManager.Instance.ClaimAsync(RewardKeys.Heart, hearts).Forget();
        else
            HeartManager.Instance.TryAddHeart(hearts);
    }
}
