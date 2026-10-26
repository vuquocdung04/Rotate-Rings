using EventDispatcher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeartView : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private TextMeshProUGUI heartCountText;
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Optional Icons")]
    [SerializeField] private GameObject normalHeartIcon;
    [SerializeField] private GameObject unlimitedHeartIcon;

    [Header("Optional")]
    [SerializeField] private Button btnOpenOffer;

    private void Awake()
    {
        if (btnOpenOffer != null) btnOpenOffer.onClick.AddListener(OnOpenOffer);
    }

    private void Start()
    {
        timerText.BindHeartTimer(this);
    }

    private void OnOpenOffer()
    {
        if (HeartManager.Instance != null) HeartManager.Instance.TryShowHeartOffer();
    }

    private void OnEnable()
    {
        this.RegisterListener(EventID.CHANGE_HEART, OnHeartChanged);
        UpdateHeartStateVisuals();
    }

    private void OnDisable()
    {
        this.RemoveListener(EventID.CHANGE_HEART, OnHeartChanged);
    }
    private void OnHeartChanged(object _) => UpdateHeartStateVisuals();
    private void UpdateHeartStateVisuals()
    {
        var hm = HeartManager.Instance;
        if (hm == null) return;

        bool isUnlimited = hm.IsUnlimited;

        normalHeartIcon.SetActive(!isUnlimited);
        unlimitedHeartIcon.SetActive(isUnlimited);

        heartCountText.gameObject.SetActive(!isUnlimited);
        heartCountText.text = hm.CurrentHeart.ToString();
    }
}