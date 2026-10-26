using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WinBox : BaseBox
{
    [Serializable]
    private class Row
    {
        public GameObject root;
        public Image icon;
        public TMP_Text label;
        public TMP_Text value;
        public Transform coinIcon;
    }

    [SerializeField] private GameObject holder;
    public Button btnNext;

    [Header("Rows")]
    [SerializeField] private Row rowSales;

    [Header("Total")]
    [SerializeField] private RectTransform totalRoot;
    [SerializeField] private TMP_Text textTotal;
    [SerializeField] private Transform totalCoin;

    [Header("Receipt")]
    [SerializeField] private TMP_Text txtDate;
    [SerializeField] private TMP_Text txtDay;

    [Header("Celebration")]
    [SerializeField] private WinCelebration celebration;
    [SerializeField] private float holderDelay;
    [SerializeField] private RectTransform ribbon;
    [SerializeField] private Transform title;
    [SerializeField] private float totalRiseDuration = 0.5f;

    [Header("Juice")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform[] shines;
    [SerializeField] private Transform[] decorStars;
    [SerializeField] private RectTransform buttonsRoot;
    [SerializeField] private UIConfettiBurst confetti;
    [SerializeField] private UIBurstEffect sparkle;
    [SerializeField] private RectTransform stamp;
    [SerializeField] private float panelDrop = 280f;
    [SerializeField] private float rowPopDuration = 0.22f;
    [SerializeField] private float countDuration = 0.45f;
    [SerializeField] private float hitPunch = 0.15f;
    [SerializeField] private float stampAngle = -10f;
    [SerializeField] private int totalConfetti = 22;
    [SerializeField] private float buttonBreath = 1.04f;

    [Header("Coin Fly")]
    [SerializeField] private WinDrinkItem itemPrefab;
    [SerializeField] private Transform itemParent;
    [SerializeField] private Sprite spriteCoin;
    [SerializeField] private int rowCoinCount = 5;
    [SerializeField] private float rowSpawnRadius = 30f;
    [SerializeField] private float itemDelayStep = 0.05f;
    [SerializeField] private float rowNextDelay = 0.3f;

    [Header("Sound")]
    [SerializeField] private string soundReveal = AudioKeys.SFX_WIN;
    [SerializeField] private string soundConfetti = AudioKeys.SFX_REWARD_GIFTBOX;
    [SerializeField] private string soundTick = AudioKeys.COINS;
    [SerializeField] private string soundTotal = AudioKeys.COINS;
    [SerializeField] private string soundStamp;

    private readonly List<WinDrinkItem> items = new();
    private readonly List<Tween> loops = new();
    private Sequence sequence;
    private Vector2 panelBase;
    private bool cached;
    private CanvasGroup stampGroup;
    private int runningTotal;
    private float pendingFlight;
    private int coin;
    private int level = -1;
    private float lastTick;
    private Vector3 ribbonScale = Vector3.one;

    public void Setup(int rewardCoin, int playedLevel = -1)
    {
        coin = Mathf.Max(0, rewardCoin);
        level = playedLevel;
    }

    protected override void InitState() => Play();

    protected override void OnDestroy()
    {
        Kill();
        base.OnDestroy();
    }

    private void Play()
    {
        Cache();
        Kill();
        ResetState();

        sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true).SetLink(gameObject);

        float holderAt = holderDelay;
        if (celebration != null)
        {
            if (holder != null) holder.SetActive(false);
            celebration.ResetState();
            sequence.Insert(0f, celebration.CreateIntro());
            holderAt = Mathf.Max(holderDelay, celebration.RevealAt);
        }

        sequence.InsertCallback(holderAt, ShowHolder);
        sequence.Insert(holderAt, CreateIntro());
        AppendRow(rowSales, coin);
        AppendTotal();
        AppendStamp();
        sequence.AppendCallback(ShowButtons);
    }

    private void Cache()
    {
        if (cached) return;
        cached = true;

        if (panel != null) panelBase = panel.anchoredPosition;
        if (ribbon != null) ribbonScale = ribbon.localScale;

        if (stamp != null)
        {
            stampGroup = stamp.GetComponent<CanvasGroup>();
            if (stampGroup == null) stampGroup = stamp.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void ResetState()
    {
        if (holder != null) holder.SetActive(celebration == null);
        if (txtDate != null) txtDate.text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm");
        if (txtDay != null) txtDay.text = LocalizationManager.Get(LocalizationKeys.UI_LEVEL, level > 0 ? level : UseProfile.Level);
        ResetRow(rowSales);

        if (ribbon != null)
        {
            ribbon.DOKill();
            ribbon.localScale = Vector3.zero;
        }

        if (textTotal != null) textTotal.text = "0";
        runningTotal = 0;
        pendingFlight = 0f;

        if (panel != null)
        {
            panel.DOKill();
            panel.anchoredPosition = panelBase + new Vector2(0f, panelDrop);
            panel.localScale = Vector3.one * 0.8f;
        }

        if (shines != null)
        {
            foreach (RectTransform s in shines)
                if (s != null) s.localScale = Vector3.zero;
        }

        if (decorStars != null)
        {
            foreach (Transform s in decorStars)
            {
                if (s == null) continue;
                s.DOKill();
                s.localScale = Vector3.zero;
                s.localRotation = Quaternion.identity;
            }
        }

        SetScale(totalRoot, Vector3.zero);
        SetScale(buttonsRoot, Vector3.zero);

        if (stamp != null)
        {
            stamp.DOKill();
            stamp.localScale = Vector3.one;
            stamp.localRotation = Quaternion.Euler(0f, 0f, stampAngle);
        }

        if (stampGroup != null) stampGroup.alpha = 0f;
        if (btnNext != null) btnNext.interactable = false;
        if (confetti != null) confetti.Clear();
    }

    private static void ResetRow(Row row)
    {
        if (row == null) return;
        if (row.value != null) row.value.text = "0";
        SetScale(row.root != null ? row.root.transform : null, Vector3.zero);
    }

    private Sequence CreateIntro()
    {
        Sequence intro = DOTween.Sequence();

        if (panel != null)
        {
            intro.Insert(0f, panel.DOAnchorPos(panelBase, 0.45f).SetEase(Ease.OutBack, 1.3f));
            intro.Insert(0f, panel.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
            intro.Insert(0.45f, panel.DOPunchScale(new Vector3(0.05f, -0.04f, 0f), 0.3f, 5, 0.6f));
        }

        if (shines != null)
        {
            for (int i = 0; i < shines.Length; i++)
                if (shines[i] != null) intro.Insert(0.1f + i * 0.08f, shines[i].DOScale(1f, 0.6f).SetEase(Ease.OutCubic));
        }

        if (decorStars != null)
        {
            for (int i = 0; i < decorStars.Length; i++)
                if (decorStars[i] != null) intro.Insert(0.3f + i * 0.06f, decorStars[i].DOScale(1f, 0.28f).SetEase(Ease.OutBack));
        }

        if (ribbon != null) intro.Insert(0.22f, ribbon.DOScale(ribbonScale, 0.32f).SetEase(Ease.OutBack, 2.6f));
        if (title != null) intro.Insert(0.4f, title.DOPunchScale(Vector3.one * 0.18f, 0.3f, 6, 0.5f));

        intro.InsertCallback(0.4f, () =>
        {
            if (confetti != null) confetti.Play();
            PlaySound(soundConfetti);
        });

        if (totalRoot != null) intro.Insert(0.45f, totalRoot.DOScale(1f, 0.3f).SetEase(Ease.OutBack));

        return intro;
    }

    private void AppendRow(Row row, int target)
    {
        if (row == null || row.value == null) return;

        TMP_Text text = row.value;
        if (row.root != null) sequence.Append(row.root.transform.DOScale(1f, rowPopDuration).SetEase(Ease.OutBack, 2f));

        int shown = 0;
        sequence.Join(DOTween.To(() => shown, x =>
        {
            shown = x;
            text.text = x.ToString();
        }, target, countDuration).SetEase(Ease.OutCubic));
        sequence.AppendCallback(() =>
        {
            Hit(text.transform);
            Sparkle(row.coinIcon);
        });

        int from = runningTotal;
        runningTotal += target;
        int to = runningTotal;
        int count = itemPrefab != null && row.coinIcon != null && totalCoin != null ? Mathf.Clamp(target, 0, rowCoinCount) : 0;

        if (count <= 0)
        {
            sequence.AppendCallback(() => SetTotal(to));
            return;
        }

        float flight = (count - 1) * itemDelayStep + itemPrefab.Duration;
        float wait = Mathf.Min(flight, rowNextDelay);
        sequence.AppendCallback(() => SpawnCoins(row.coinIcon, count, from, to));
        sequence.AppendInterval(wait);
        pendingFlight = flight - wait;
    }

    private void SpawnCoins(Transform start, int count, int from, int to)
    {
        Transform parent = itemParent != null ? itemParent : transform;
        int arrived = 0;

        for (int i = 0; i < count; i++)
        {
            WinDrinkItem item = Instantiate(itemPrefab, parent);
            items.Add(item);
            item.SetSprite(spriteCoin);
            item.SetSpawn(start, rowSpawnRadius);
            item.FlyTo(totalCoin, i * itemDelayStep, done =>
            {
                arrived++;
                Hit(totalCoin);
                items.Remove(done);
                if (done != null) Destroy(done.gameObject);
                SetTotal(from + (to - from) * arrived / count);
            });
        }
    }

    private void SetTotal(int value)
    {
        if (textTotal == null) return;

        textTotal.text = value.ToString();
        if (!DOTween.IsTweening(textTotal.transform)) Hit(textTotal.transform);

        if (Time.unscaledTime - lastTick < 0.05f) return;
        lastTick = Time.unscaledTime;
        PlaySound(soundTick);
    }

    private void ShowHolder()
    {
        if (holder != null) holder.SetActive(true);
        PlaySound(soundReveal);
        if (celebration != null) celebration.Reveal();
    }

    private void AppendTotal()
    {
        if (pendingFlight > 0f) sequence.AppendInterval(pendingFlight);
        pendingFlight = 0f;

        if (textTotal != null && coin > runningTotal)
        {
            int last = runningTotal;
            float shown = runningTotal;
            sequence.Append(DOTween.To(() => shown, x =>
            {
                shown = x;
                int value = Mathf.RoundToInt(x);
                if (value == last) return;
                last = value;
                SetTotal(value);
            }, coin, totalRiseDuration).SetEase(Ease.OutCubic));
            runningTotal = coin;
        }

        sequence.AppendCallback(() =>
        {
            SetTotal(runningTotal);
            PlaySound(soundTotal);

            if (totalRoot != null)
            {
                totalRoot.DOKill(true);
                totalRoot.DOPunchScale(Vector3.one * 0.18f, 0.45f, 6, 0.5f).SetUpdate(true);
                if (confetti != null) confetti.BurstAt(totalRoot.position, totalConfetti);
            }

            Sparkle(totalCoin);
        });
        sequence.AppendInterval(0.1f);
    }

    private void AppendStamp()
    {
        if (stamp == null || stampGroup == null) return;

        sequence.AppendInterval(0.15f);
        sequence.AppendCallback(() =>
        {
            stamp.localScale = Vector3.one * 2.3f;
            stamp.localRotation = Quaternion.Euler(0f, 0f, stampAngle - 14f);
        });
        sequence.Append(stamp.DOScale(1f, 0.16f).SetEase(Ease.InQuad));
        sequence.Join(stamp.DOLocalRotate(new Vector3(0f, 0f, stampAngle), 0.16f).SetEase(Ease.InQuad));
        sequence.Join(stampGroup.DOFade(1f, 0.08f));
        sequence.AppendCallback(() =>
        {
            PlaySound(soundStamp);

            if (panel != null)
            {
                panel.DOKill(true);
                panel.DOPunchAnchorPos(new Vector2(0f, -14f), 0.28f, 10, 0.6f).SetUpdate(true);
            }

            Sparkle(stamp);
            if (confetti != null) confetti.BurstAt(stamp.position, 12, 90f, 80f, 0.45f);
        });
        sequence.Append(stamp.DOPunchScale(new Vector3(0.12f, -0.1f, 0f), 0.3f, 6, 0.6f));
    }

    private void ShowButtons()
    {
        if (btnNext != null) btnNext.interactable = true;
        if (buttonsRoot == null) return;

        buttonsRoot.DOKill();
        buttonsRoot.localScale = Vector3.zero;
        buttonsRoot.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2f).SetUpdate(true).OnComplete(() =>
        {
            loops.Add(buttonsRoot.DOScale(buttonBreath, 0.7f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true));
        });
    }

    private void Hit(Transform target)
    {
        if (target == null) return;

        target.DOKill(true);
        target.DOPunchScale(Vector3.one * hitPunch, 0.2f, 1, 0f).SetUpdate(true);
    }

    private void Sparkle(Transform at)
    {
        if (sparkle != null && at != null) sparkle.Play(at.position);
    }

    private static void SetScale(Transform target, Vector3 scale)
    {
        if (target == null) return;

        target.DOKill();
        target.localScale = scale;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key);
    }

    private void Kill()
    {
        sequence?.Kill();
        sequence = null;

        foreach (Tween l in loops) l?.Kill();
        loops.Clear();

        foreach (WinDrinkItem item in items)
        {
            if (item == null) continue;
            item.Stop();
            Destroy(item.gameObject);
        }

        items.Clear();
    }
}
