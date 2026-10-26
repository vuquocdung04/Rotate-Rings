using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RewardBox : BaseBox, IPopupScale
{
    [Header("Reward Items")]
    public RewardItemView rewardItemPrefab;
    public RectTransform rewardItemParent;
    public ScrollRect scroll;
    public GridLayoutGroup grid;
    [Min(1)] public int maxColumns = 3;
    public Vector2 cellLarge = new(250f, 250f);
    public Vector2 cellSmall = new(205f, 205f);

    [Header("Claim")]
    public Button btnClaim;
    public GameObject txtTapToContinue;

    [Header("Stage")]
    public RectTransform[] rays;
    public float[] raySpeeds = { 14f, -9f };
    public Graphic glow;
    public RectTransform title;
    public Graphic description;
    public UIBurstEffect itemBurst;

    [Header("Congrats")]
    public ParticleSystem[] congrats;
    [Min(0f)] public float congratsDelay = 0.1f;
    [Min(0f)] public float congratsInterval = 0.22f;

    [Header("Reveal")]
    [Min(0f)] public float titleDrop = 160f;
    [Min(0f)] public float itemsDelay = 0.35f;
    [Min(0f)] public float revealDelay = 0.05f;
    [Min(0.01f)] public float revealDuration = 0.22f;
    [Min(0f)] public float countDuration = 0.5f;
    public float wobbleAngle = 12f;

    [Header("Auto Scroll")]
    [Min(0f)] public float autoScrollDelay = 0.05f;
    [Min(1f)] public float autoScrollSpeed = 1200f;
    [Min(0.01f)] public float autoScrollMinDuration = 0.35f;

    [Header("Sound")]
    public string soundOpen = AudioKeys.SFX_REWARD_GIFTBOX;
    public string soundItem = AudioKeys.COINS;

    private readonly List<RewardItemView> _items = new();
    private readonly List<GameReward> _rewards = new();
    private Coroutine _reveal;
    private Coroutine _congrats;
    private Sequence _stage;
    private Tween _glowPulse;
    private Tween _tapPulse;
    private Vector2 _titleBase;
    private bool _titleCached;
    private bool _claimable;

    protected override void Init()
    {
        if (btnClaim != null) btnClaim.onClick.AddListener(OnClaim);
    }

    private void Update()
    {
        if (rays == null) return;

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < rays.Length; i++)
        {
            if (rays[i] != null) rays[i].Rotate(0f, 0f, (i < raySpeeds.Length ? raySpeeds[i] : 10f) * dt);
        }
    }

    protected override void OnDestroy()
    {
        StopAll();
        base.OnDestroy();
    }

    public void Populate(IReadOnlyList<GameReward> rewards)
    {
        StopAll();

        _items.ClearSpawned();
        _rewards.Clear();
        if (rewards != null) _rewards.AddRange(rewards);

        for (int i = 0; i < _rewards.Count; i++)
        {
            RewardItemView item = Instantiate(rewardItemPrefab, rewardItemParent);
            item.Setup(_rewards[i]);
            _items.Add(item);
        }

        ApplyGrid();
        Play();
    }

    private void OnClaim()
    {
        if (_claimable) Close();
    }

    private void Play()
    {
        RefreshLayout();
        SetClaimable(false);

        if (_items.Count == 0)
        {
            SetClaimable(true);
            return;
        }

        PlayStage();
        PlayCongrats();
        _reveal = StartCoroutine(Reveal());
    }

    private void PlayStage()
    {
        _stage = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        PlaySound(soundOpen);

        if (rays != null)
        {
            for (int i = 0; i < rays.Length; i++)
            {
                if (rays[i] == null) continue;

                rays[i].localScale = Vector3.zero;
                _stage.Insert(0.05f * i, rays[i].DOScale(1f, 0.6f).SetEase(Ease.OutBack));
            }
        }

        if (glow != null)
        {
            Transform glowTransform = glow.transform;
            glowTransform.localScale = Vector3.one * 0.6f;
            _stage.Insert(0f, glowTransform.DOScale(1f, 0.5f).SetEase(Ease.OutCubic));
            _stage.InsertCallback(0.5f, () =>
                _glowPulse = glowTransform.DOScale(1.08f, 0.9f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(gameObject));
        }

        if (title != null)
        {
            if (!_titleCached)
            {
                _titleBase = title.anchoredPosition;
                _titleCached = true;
            }

            title.anchoredPosition = _titleBase + new Vector2(0f, titleDrop);
            title.localScale = new Vector3(1.25f, 1.25f, 1f);
            _stage.Insert(0.05f, title.DOAnchorPos(_titleBase, 0.4f).SetEase(Ease.OutBack));
            _stage.Insert(0.05f, title.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            _stage.Insert(0.45f, title.DOPunchScale(new Vector3(0.08f, -0.08f, 0f), 0.3f, 6, 0.6f));
        }

        if (description != null) description.canvasRenderer.SetAlpha(0f);
    }

    private void PlayCongrats()
    {
        if (congrats == null || congrats.Length == 0) return;

        for (int i = 0; i < congrats.Length; i++)
        {
            if (congrats[i] != null) congrats[i].Clear(true);
        }

        _congrats = StartCoroutine(Congrats());
    }

    private IEnumerator Congrats()
    {
        if (congratsDelay > 0f) yield return new WaitForSecondsRealtime(congratsDelay);

        for (int i = 0; i < congrats.Length; i++)
        {
            if (congrats[i] != null) congrats[i].Play(true);

            if (i < congrats.Length - 1 && congratsInterval > 0f)
                yield return new WaitForSecondsRealtime(congratsInterval);
        }

        _congrats = null;
    }

    private void ApplyGrid()
    {
        if (grid == null) return;

        grid.cellSize = _items.Count <= 2 ? cellLarge : cellSmall;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Clamp(_items.Count, 1, maxColumns);
    }

    private void RefreshLayout()
    {
        if (rewardItemParent == null) return;

        float overflow = ScrollDistance();

        if (scroll == null) return;

        scroll.StopMovement();
        scroll.horizontal = false;
        scroll.vertical = overflow > 0.5f;
        rewardItemParent.anchoredPosition = new Vector2(rewardItemParent.anchoredPosition.x, overflow > 0.5f ? 0f : overflow * 0.5f);
    }

    private float ScrollDistance()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rewardItemParent);

        if (scroll == null) return 0f;

        RectTransform viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        return rewardItemParent.rect.height - viewport.rect.height;
    }

    private IEnumerator Reveal()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].transform.localScale = Vector3.zero;
            _items[i].transform.localRotation = Quaternion.identity;
        }

        yield return null;
        RefreshLayout();

        if (itemsDelay > 0f) yield return new WaitForSecondsRealtime(itemsDelay);

        for (int i = 0; i < _items.Count; i++)
        {
            RevealItem(i);

            if (i < _items.Count - 1 && revealDelay > 0f)
                yield return new WaitForSecondsRealtime(revealDelay);
        }

        yield return new WaitForSecondsRealtime(revealDuration);

        float distance = ScrollDistance();
        if (scroll != null && distance > 0.5f) yield return AutoScroll(distance);

        if (description != null) description.CrossFadeAlpha(1f, 0.25f, true);

        _reveal = null;
        SetClaimable(true);
    }

    private void RevealItem(int index)
    {
        Transform item = _items[index].transform;

        DOTween.Sequence()
            .Append(item.DOScale(1.2f, revealDuration * 0.55f).SetEase(Ease.OutBack))
            .Append(item.DOScale(1f, revealDuration * 0.45f).SetEase(Ease.OutQuad))
            .Insert(0f, item.DOPunchRotation(new Vector3(0f, 0f, wobbleAngle), revealDuration * 1.5f, 6, 0.5f))
            .SetUpdate(true)
            .SetLink(item.gameObject);

        PlaySound(soundItem, 1f + 0.08f * index);

        if (itemBurst != null) itemBurst.Play(item.position);

        CountUp(index);
    }

    private void CountUp(int index)
    {
        RewardItemView item = _items[index];
        GameReward reward = _rewards[index];

        if (item.txtQuantity == null || reward == null || reward.quantity <= 1 || countDuration <= 0f) return;

        RewardManager manager = RewardManager.Instance;
        if (manager == null || manager.IsFlag(reward.id)) return;

        DOVirtual.Float(0f, reward.quantity, countDuration, v =>
                item.txtQuantity.text = manager.Format(reward.id, Mathf.RoundToInt(v)))
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetLink(item.gameObject)
            .OnComplete(() => item.txtQuantity.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6, 0.6f).SetUpdate(true));
    }

    private IEnumerator AutoScroll(float distance)
    {
        float duration = Mathf.Max(autoScrollMinDuration, distance / autoScrollSpeed);

        if (autoScrollDelay > 0f) yield return new WaitForSecondsRealtime(autoScrollDelay);

        scroll.StopMovement();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            scroll.verticalNormalizedPosition = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        scroll.verticalNormalizedPosition = 0f;
    }

    private void StopAll()
    {
        if (_reveal != null) StopCoroutine(_reveal);
        if (_congrats != null) StopCoroutine(_congrats);
        _reveal = null;
        _congrats = null;

        _stage?.Kill();
        _glowPulse?.Kill();
        _stage = null;
        _glowPulse = null;
    }

    private void SetClaimable(bool value)
    {
        _claimable = value;

        if (txtTapToContinue == null) return;

        txtTapToContinue.SetActive(value);

        _tapPulse?.Kill();
        _tapPulse = null;
        txtTapToContinue.transform.localScale = Vector3.one;

        if (value)
            _tapPulse = txtTapToContinue.transform.DOScale(1.08f, 0.6f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(txtTapToContinue);
    }

    private static void PlaySound(string key, float pitch = 1f)
    {
        if (!string.IsNullOrEmpty(key) && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key, pitch);
    }
}
