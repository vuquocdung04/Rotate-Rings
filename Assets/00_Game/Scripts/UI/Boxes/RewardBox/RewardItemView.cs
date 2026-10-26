using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardItemView : MonoBehaviour
{
    public Image iconReward;
    public TextMeshProUGUI txtQuantity;

    public void Setup(GameReward reward)
    {
        if (reward == null) return;

        RewardManager manager = RewardManager.Instance;

        if (iconReward != null)
        {
            Sprite icon = manager != null ? manager.GetIcon(reward.id) : null;

            iconReward.sprite = icon;
            iconReward.preserveAspect = true;
            iconReward.enabled = icon != null;
        }

        if (txtQuantity != null)
        {
            txtQuantity.text = manager != null
                ? manager.Format(reward.id, reward.quantity)
                : reward.quantity.ToString();
        }
    }

    public Tween DoScale(float duration)
    {
        transform.localScale = Vector3.zero;

        return transform.DOScale(1f, duration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }
}
