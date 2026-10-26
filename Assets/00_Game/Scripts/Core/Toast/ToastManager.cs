using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance;
    public List<TextMeshProUGUI> toastTexts;

    [Header("Config")]
    public float flyUpDistance = 200f;
    public float scaleUpDuration = 0.3f;
    public float holdDuration = 0.3f;
    public float flyUpDuration = 0.5f;
    public float fadeOutDuration = 0.5f;
    public Ease scaleEase = Ease.OutBack;
    public Ease flyEase = Ease.InSine;

    private Dictionary<TextMeshProUGUI, (CanvasGroup cg, Tween tween)> _activeTweens = new();
    private List<CanvasGroup> _canvasGroups = new();
    private AudioManager audioManager;

    public void Init(AudioManager audioManager)
    {
        Instance = this;
        this.audioManager = audioManager;
        foreach (var txt in toastTexts)
        {
            var cg = txt.GetComponent<CanvasGroup>();
            if (cg == null) cg = txt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            _canvasGroups.Add(cg);
        }
    }

    public void ShowToast(string toastName)
    {
        int index = GetAvailableIndex();
        if (index < 0) return;

        var txt = toastTexts[index];
        var cg = _canvasGroups[index];

        if (_activeTweens.TryGetValue(txt, out var old))
            old.tween?.Kill();

        txt.text = toastName;
        audioManager.PlaySfx(AudioKeys.TOAST);

        var rect = txt.rectTransform;
        rect.anchoredPosition = Vector2.zero;       
        rect.localScale = Vector3.zero;              
        cg.alpha = 1f;

        var tween = DOTween.Sequence()
            // 1) Scale 0 → 1 tại chỗ
            .Append(rect.DOScale(Vector3.one, scaleUpDuration).SetEase(scaleEase))
            // 2) Đợi 1 nhịp
            .AppendInterval(holdDuration)
            // 3) Bay lên + mờ dần cùng lúc
            .Append(rect.DOAnchorPosY(flyUpDistance, flyUpDuration).SetEase(flyEase))
            .Join(cg.DOFade(0f, fadeOutDuration))
            // 4) Cleanup
            .OnComplete(() =>
            {
                rect.localScale = Vector3.zero;
                _activeTweens.Remove(txt);
            });

        _activeTweens[txt] = (cg, tween);
    }

    private int GetAvailableIndex()
    {
        for (int i = 0; i < _canvasGroups.Count; i++)
            if (!_activeTweens.ContainsKey(toastTexts[i]))
                return i;
        return -1;
    }
}