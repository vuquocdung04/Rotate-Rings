using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoseBox : BaseBox
{
    public Button btnTryAgain;
    public Button btnBackToLobby;

    [Header("Content")]
    [SerializeField] private TMP_Text txtDay;
    [SerializeField] private RectTransform title;
    [SerializeField] private RectTransform tryHolder;
    [SerializeField] private RectTransform closeButton;
    [SerializeField] private Graphic hint;

    [Header("Hero")]
    [SerializeField] private RectTransform hero;
    [SerializeField] private Image barista;
    [SerializeField] private Sprite[] cryFrames;
    [SerializeField] private float fps = 20f;
    [SerializeField] private Image glow;

    [Header("Rain")]
    [SerializeField] private RectTransform cloud;
    [SerializeField] private Image cloudImage;
    [SerializeField] private Color flashColor = new Color(1f, 1f, 0.9f, 1f);
    [SerializeField] private Vector2 flashGap = new Vector2(2f, 4.5f);
    [SerializeField] private RectTransform rainArea;
    [SerializeField] private Sprite dropSprite;
    [SerializeField] private Color dropColor = Color.white;
    [SerializeField] private Vector2 dropSize = new Vector2(10f, 15f);
    [SerializeField] private int dropCount = 22;
    [SerializeField] private Vector2 dropTime = new Vector2(0.55f, 0.75f);
    [SerializeField, Range(0f, 1f)] private float slant = 0.25f;

    [Header("Juice")]
    [SerializeField] private float buttonBreath = 1.05f;
    [SerializeField] private float buttonWiggleInterval = 2.2f;

    [Header("Sound")]
    [SerializeField] private string soundSad = AudioKeys.SFX_LOSE;

    private readonly List<Tween> loops = new();
    private readonly List<Image> drops = new();

    private Sequence intro;
    private Tween flash;
    private bool cached;
    private bool animating;
    private int frame;
    private float frameTimer;
    private int level = -1;

    private CanvasGroup dayGroup;
    private CanvasGroup closeGroup;
    private CanvasGroup cloudGroup;
    private Quaternion titleRot;
    private Vector2 heroBase;
    private Vector2 cloudBase;
    private Color cloudColor;
    private float glowAlpha;
    private float hintAlpha;

    public void Setup(int playedLevel) => level = playedLevel;

    protected override void Init() => EnsureCache();

    protected override void InitState()
    {
        if (txtDay != null)
            txtDay.text = LocalizationManager.Get(LocalizationKeys.UI_LEVEL, level > 0 ? level : UseProfile.Level);

        Play();
    }

    protected override void OnDestroy()
    {
        KillAll();
        base.OnDestroy();
    }

    private void Update()
    {
        if (animating) TickBarista(Time.unscaledDeltaTime);
    }

    private void EnsureCache()
    {
        if (cached) return;
        cached = true;

        if (txtDay != null) dayGroup = GetGroup(txtDay.transform);

        closeGroup = GetGroup(closeButton);
        cloudGroup = GetGroup(cloud);

        if (title != null) titleRot = title.localRotation;
        if (hero != null) heroBase = hero.anchoredPosition;
        if (cloud != null) cloudBase = cloud.anchoredPosition;
        if (cloudImage != null) cloudColor = cloudImage.color;
        if (glow != null) glowAlpha = glow.color.a;
        if (hint != null) hintAlpha = hint.color.a;
    }

    private static CanvasGroup GetGroup(Transform target)
    {
        if (target == null) return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
    }

    private void Play()
    {
        EnsureCache();
        KillAll();
        ResetJuice();

        intro = DOTween.Sequence().SetTarget(this).SetUpdate(true).SetLink(gameObject);

        if (dayGroup != null)
        {
            intro.Insert(0f, dayGroup.DOFade(1f, 0.25f));
            intro.Insert(0f, dayGroup.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 2f));
        }

        if (title != null)
        {
            intro.Insert(0.08f, title.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 2.4f));
            intro.Insert(0.3f, title.DOPunchRotation(new Vector3(0f, 0f, -5f), 0.5f, 6, 0.5f));
        }

        intro.InsertCallback(0.2f, () => PlaySound(soundSad));

        if (glow != null) intro.Insert(0.15f, glow.DOFade(glowAlpha, 0.4f));

        if (hero != null)
        {
            intro.Insert(0.18f, hero.DOAnchorPos(heroBase, 0.3f).SetEase(Ease.InQuad));
            intro.Insert(0.18f, hero.DOScale(1f, 0.22f).SetEase(Ease.OutQuad));
            intro.InsertCallback(0.48f, () => hero.localScale = new Vector3(1.15f, 0.85f, 1f));
            intro.Insert(0.48f, hero.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutElastic, 1.1f, 0.35f));
        }

        intro.InsertCallback(0.18f, StartCrying);

        if (cloud != null)
        {
            intro.Insert(0.45f, cloud.DOAnchorPos(cloudBase, 0.45f).SetEase(Ease.OutBack, 1.4f));
            if (cloudGroup != null) intro.Insert(0.45f, cloudGroup.DOFade(1f, 0.3f));
        }

        intro.InsertCallback(0.75f, StartRain);

        if (tryHolder != null) intro.Insert(0.7f, tryHolder.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2f));
        if (closeGroup != null) intro.Insert(0.85f, closeGroup.DOFade(1f, 0.25f));
        if (hint != null) intro.Insert(0.95f, hint.DOFade(hintAlpha, 0.3f));

        intro.OnComplete(StartIdle);
    }

    private void ResetJuice()
    {
        if (dayGroup != null)
        {
            dayGroup.alpha = 0f;
            dayGroup.transform.localScale = Vector3.one * 0.6f;
        }

        if (title != null)
        {
            title.localScale = Vector3.zero;
            title.localRotation = titleRot;
        }

        if (hero != null)
        {
            hero.anchoredPosition = heroBase + new Vector2(0f, 140f);
            hero.localScale = Vector3.one * 0.6f;
        }

        if (cloud != null)
        {
            cloud.anchoredPosition = cloudBase + new Vector2(0f, 120f);
            cloud.localScale = Vector3.one;
            cloud.localRotation = Quaternion.identity;
        }

        ResetCloudColor();

        if (cloudGroup != null) cloudGroup.alpha = 0f;

        SetAlpha(glow, 0f);
        SetAlpha(hint, 0f);

        if (tryHolder != null)
        {
            tryHolder.localScale = Vector3.zero;
            tryHolder.localRotation = Quaternion.identity;
        }

        if (closeGroup != null) closeGroup.alpha = 0f;

        HideDrops();
        animating = false;
        ShowFrame(0);
    }

    private void StartCrying()
    {
        frame = 0;
        frameTimer = 0f;
        ShowFrame(0);
        animating = barista != null && cryFrames != null && cryFrames.Length > 1;
    }

    private void TickBarista(float dt)
    {
        float step = 1f / Mathf.Max(1f, fps);

        frameTimer += dt;
        if (frameTimer < step) return;

        frameTimer = Mathf.Min(frameTimer - step, step);
        frame = (frame + 1) % cryFrames.Length;
        ShowFrame(frame);
    }

    private void ShowFrame(int index)
    {
        if (barista == null || cryFrames == null || cryFrames.Length == 0) return;

        Sprite sprite = cryFrames[Mathf.Clamp(index, 0, cryFrames.Length - 1)];
        if (sprite != null && barista.sprite != sprite) barista.sprite = sprite;
    }

    private void StartRain()
    {
        if (rainArea == null || dropSprite == null) return;

        Rect area = rainArea.rect;
        float lane = area.width / Mathf.Max(1, dropCount);
        Vector2 travel = new Vector2(-slant * area.height, -area.height);
        Quaternion tilt = Quaternion.Euler(0f, 0f, -Mathf.Atan(slant) * Mathf.Rad2Deg);

        for (int i = 0; i < dropCount; i++)
        {
            Image drop = GetDrop(i);
            RectTransform rt = drop.rectTransform;
            float time = Random.Range(dropTime.x, dropTime.y);
            float laneMin = area.xMin + lane * i;
            Vector2 start = Vector2.zero;

            rt.localRotation = tilt;

            Sequence fall = DOTween.Sequence().SetUpdate(true).SetLink(drop.gameObject);

            fall.AppendCallback(() =>
            {
                start = new Vector2(Random.Range(laneMin, laneMin + lane), area.yMax);
                rt.anchoredPosition = start;

                Color color = drop.color;
                color.a = 0f;
                drop.color = color;
            });
            fall.Append(DOVirtual.Float(0f, 1f, time, t => rt.anchoredPosition = start + travel * t).SetEase(Ease.Linear));
            fall.Insert(0f, drop.DOFade(dropColor.a, time * 0.15f));
            fall.Insert(time * 0.65f, drop.DOFade(0f, time * 0.35f));
            fall.AppendInterval(Random.Range(0f, 0.1f));
            fall.SetLoops(-1, LoopType.Restart);
            fall.SetDelay(Random.Range(0f, time));

            drop.gameObject.SetActive(true);
            SetAlpha(drop, 0f);
            loops.Add(fall);
        }
    }

    private Image GetDrop(int index)
    {
        while (drops.Count <= index)
        {
            var go = new GameObject("Drop", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(rainArea, false);

            Image image = go.GetComponent<Image>();
            RectTransform rt = image.rectTransform;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            image.raycastTarget = false;
            drops.Add(image);
        }

        Image drop = drops[index];
        drop.sprite = dropSprite;
        drop.rectTransform.sizeDelta = dropSize;
        drop.color = dropColor;

        return drop;
    }

    private void HideDrops()
    {
        for (int i = 0; i < drops.Count; i++)
        {
            if (drops[i] == null) continue;

            drops[i].DOKill();
            drops[i].rectTransform.DOKill();
            drops[i].gameObject.SetActive(false);
        }
    }

    private void StartIdle()
    {
        intro = null;

        if (hero != null)
        {
            loops.Add(hero.DOScale(new Vector3(1.03f, 0.96f, 1f), 0.45f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(hero.gameObject));
        }

        if (cloud != null)
        {
            loops.Add(cloud.DOAnchorPosY(cloudBase.y + 8f, 1.1f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(cloud.gameObject));
            loops.Add(cloud.DOScale(new Vector3(1.04f, 0.96f, 1f), 0.8f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(cloud.gameObject));
        }

        ScheduleFlash(0.3f);

        if (glow != null)
        {
            loops.Add(glow.DOFade(glowAlpha * 0.7f, 1.2f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(glow.gameObject));
        }

        if (tryHolder == null) return;

        loops.Add(tryHolder.DOScale(buttonBreath, 0.6f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(tryHolder.gameObject));

        Sequence wiggle = DOTween.Sequence().SetUpdate(true).SetLink(tryHolder.gameObject);
        wiggle.AppendInterval(buttonWiggleInterval);
        wiggle.Append(tryHolder.DOPunchRotation(new Vector3(0f, 0f, 7f), 0.5f, 8, 0.6f));
        wiggle.SetLoops(-1, LoopType.Restart);
        loops.Add(wiggle);
    }

    private void ScheduleFlash(float delay)
    {
        if (cloudImage == null) return;

        Sequence s = DOTween.Sequence().SetUpdate(true).SetLink(cloudImage.gameObject);

        s.AppendInterval(delay);
        s.Append(cloudImage.DOColor(flashColor, 0.05f));
        s.Append(cloudImage.DOColor(cloudColor, 0.07f));
        s.AppendInterval(0.06f);
        s.Append(cloudImage.DOColor(flashColor, 0.04f));
        s.Append(cloudImage.DOColor(cloudColor, 0.3f).SetEase(Ease.InQuad));
        s.OnComplete(() => ScheduleFlash(Random.Range(flashGap.x, flashGap.y)));
        flash = s;
    }

    private void ResetCloudColor()
    {
        if (cloudImage == null) return;

        cloudImage.DOKill();
        cloudImage.color = cloudColor;
    }

    private void KillAll()
    {
        intro?.Kill();
        intro = null;
        flash?.Kill();
        flash = null;

        for (int i = 0; i < loops.Count; i++) loops[i]?.Kill();
        loops.Clear();

        DOTween.Kill(this);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null) return;

        graphic.DOKill();

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key);
    }
}
