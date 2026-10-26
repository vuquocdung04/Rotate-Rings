using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class WinCelebration : MonoBehaviour
{
    private const float Refresh = 1f / 60f;

    [Header("Dim")]
    [SerializeField] private Image dim;
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.6f;
    [SerializeField] private float darkenDuration = 0.3f;

    [Header("Intro")]
    [SerializeField] private CanvasGroup introRoot;
    [SerializeField] private RectTransform cloud;
    [SerializeField] private RectTransform logo;
    [SerializeField] private Image shockwave;
    [SerializeField] private float shockwaveScale = 3.4f;
    [SerializeField] private RectTransform[] cloudStars;
    [SerializeField] private RectTransform fxLayer;
    [SerializeField] private Sprite gatherSprite;
    [SerializeField] private int gatherCount = 16;
    [SerializeField] private Vector2 gatherRadius = new Vector2(380f, 720f);
    [SerializeField] private Sprite streakSprite;
    [SerializeField] private int streakCount = 12;
    [SerializeField] private UIConfettiBurst confetti;
    [SerializeField] private int logoConfetti = 30;

    [Header("Intro Timing")]
    [SerializeField] private float cloudAt = 0.22f;
    [SerializeField] private float logoAt = 0.62f;
    [SerializeField] private float outroAt = 1.2f;
    [SerializeField] private float outroDuration = 0.22f;

    [Header("Intro Sound")]
    [SerializeField] private string soundCloud = AudioKeys.SFX_REWARD_GIFTBOX;
    [SerializeField] private string soundLogo = AudioKeys.SFX_WIN;

    [Header("Reveal")]
    [SerializeField] private RectTransform[] rays;
    [SerializeField] private float raySpinSeconds = 14f;
    [SerializeField] private UIFireworks fireworks;
    [SerializeField] private RectTransform[] glints;
    [SerializeField] private Vector2 glintGap = new Vector2(1.2f, 2.4f);

    [Header("Barista")]
    [SerializeField] private Image barista;
    [SerializeField] private Sprite[] cheerFrames;
    [SerializeField] private Sprite[] loopFrames;
    [SerializeField] private float fps = 30f;
    [SerializeField] private int[] cheerHolds;
    [SerializeField] private int[] loopHolds;
    [SerializeField] private float artScale = 1f;
    [SerializeField] private float loopPause = 0.9f;

    private readonly List<Image> pool = new();
    private readonly List<Tween> loops = new();

    private Sprite[] clip;
    private int frame;
    private float frameTimer;
    private float holdLeft;
    private bool animating;

    public float RevealAt => outroAt + outroDuration * 0.5f;

    private void OnDisable() => Stop();

    private void Update()
    {
        if (animating) TickBarista(Time.unscaledDeltaTime);
    }

    public void Stop()
    {
        KillLoops();
        if (fireworks != null) fireworks.Clear();
        animating = false;
    }

    public void ResetState()
    {
        KillLoops();

        if (introRoot != null)
        {
            introRoot.DOKill();
            introRoot.alpha = 1f;
            introRoot.transform.localScale = Vector3.one;
            introRoot.gameObject.SetActive(true);
        }

        SetScale(cloud, 0f);
        SetScale(logo, 0f);

        if (shockwave != null)
        {
            shockwave.rectTransform.DOKill();
            shockwave.DOKill();
            shockwave.rectTransform.localScale = Vector3.zero;
            SetAlpha(shockwave, 0f);
        }

        if (cloudStars != null)
        {
            for (int i = 0; i < cloudStars.Length; i++) SetScale(cloudStars[i], 0f);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].rectTransform.DOKill();
            pool[i].DOKill();
            pool[i].gameObject.SetActive(false);
        }

        if (dim != null)
        {
            dim.DOKill();
            SetAlpha(dim, 0f);
        }

        if (fireworks != null) fireworks.Clear();

        animating = false;
        ShowFrame(cheerFrames, 0);
    }

    public Sequence CreateIntro()
    {
        Sequence s = DOTween.Sequence();

        s.InsertCallback(0f, Gather);

        if (cloud != null)
        {
            s.InsertCallback(cloudAt, () => PlaySound(soundCloud));
            s.Insert(cloudAt, cloud.DOScale(1f, 0.38f).SetEase(Ease.OutBack, 2.2f));
        }

        if (cloudStars != null)
        {
            for (int i = 0; i < cloudStars.Length; i++)
            {
                RectTransform star = cloudStars[i];
                if (star == null) continue;

                s.Insert(cloudAt + 0.12f + i * 0.04f, star.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2.5f));
            }

            s.InsertCallback(cloudAt + 0.4f, TwinkleStars);
        }

        if (logo != null)
        {
            s.InsertCallback(logoAt, LogoBurst);
            s.Insert(logoAt, logo.DOScale(1.18f, 0.14f).SetEase(Ease.OutQuad));
            s.Insert(logoAt + 0.14f, logo.DOScale(1f, 0.2f).SetEase(Ease.OutBack, 2f));
        }

        if (cloud != null)
            s.Insert(logoAt + 0.05f, cloud.DOPunchScale(new Vector3(0.1f, -0.08f, 0f), 0.35f, 6, 0.6f));

        if (shockwave != null)
        {
            RectTransform ring = shockwave.rectTransform;

            s.InsertCallback(logoAt, () =>
            {
                ring.localScale = Vector3.one * 0.35f;
                SetAlpha(shockwave, 1f);
            });
            s.Insert(logoAt, ring.DOScale(shockwaveScale, 0.6f).SetEase(Ease.OutCubic));
            s.Insert(logoAt + 0.15f, shockwave.DOFade(0f, 0.45f).SetEase(Ease.InQuad));
        }

        if (introRoot != null)
        {
            s.Insert(outroAt, introRoot.DOFade(0f, outroDuration).SetEase(Ease.InQuad));
            s.Insert(outroAt, introRoot.transform.DOScale(1.25f, outroDuration).SetEase(Ease.InQuad));
            s.InsertCallback(outroAt + outroDuration, () => introRoot.gameObject.SetActive(false));
        }

        return s;
    }

    public void Reveal()
    {
        if (rays != null)
        {
            for (int i = 0; i < rays.Length; i++)
            {
                RectTransform ray = rays[i];
                if (ray == null) continue;

                float dir = i % 2 == 0 ? -1f : 1f;
                float seconds = raySpinSeconds * (1f + i * 0.35f);

                loops.Add(ray.DOLocalRotate(new Vector3(0f, 0f, 360f * dir), seconds, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear).SetLoops(-1, LoopType.Incremental).SetUpdate(true).SetLink(ray.gameObject));
            }
        }

        if (fireworks != null) fireworks.Play();

        if (dim != null)
        {
            dim.DOKill();
            SetAlpha(dim, 0f);
            loops.Add(dim.DOFade(dimAlpha, darkenDuration).SetUpdate(true).SetLink(dim.gameObject));
        }

        TwinkleGlints();

        clip = cheerFrames;
        frame = 0;
        frameTimer = 0f;
        holdLeft = 0f;
        ShowFrame(clip, 0);
        animating = barista != null;
    }

    private void TwinkleGlints()
    {
        if (glints == null) return;

        for (int i = 0; i < glints.Length; i++)
        {
            RectTransform glint = glints[i];
            if (glint == null) continue;

            glint.localScale = Vector3.zero;

            Sequence flash = DOTween.Sequence()
                .Append(glint.DOScale(1f, 0.18f).SetEase(Ease.OutBack, 2f))
                .Join(glint.DOLocalRotate(new Vector3(0f, 0f, 45f), 0.45f, RotateMode.LocalAxisAdd).SetEase(Ease.OutQuad))
                .Append(glint.DOScale(0f, 0.27f).SetEase(Ease.InQuad))
                .AppendInterval(Random.Range(glintGap.x, glintGap.y))
                .SetDelay(0.5f + i * 0.55f)
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(glint.gameObject);

            loops.Add(flash);
        }
    }

    private void Gather()
    {
        if (gatherSprite == null || fxLayer == null) return;

        Vector2 center = CloudCenter();

        for (int i = 0; i < gatherCount; i++)
        {
            Image image = Get(gatherSprite);
            RectTransform rt = image.rectTransform;

            float angle = (360f / gatherCount * i + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
            Vector2 from = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(gatherRadius.x, gatherRadius.y);
            float size = Random.Range(40f, 78f);
            float delay = Random.Range(0f, 0.12f);
            float duration = Random.Range(0.38f, 0.5f);

            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = from;
            rt.localScale = Vector3.zero;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            image.color = new Color(1f, 0.9f, 0.45f, 1f);

            Sequence s = DOTween.Sequence().SetTarget(image).SetUpdate(true).SetLink(image.gameObject);

            s.Insert(delay, rt.DOScale(1f, duration * 0.35f).SetEase(Ease.OutBack));
            s.Insert(delay, rt.DOAnchorPos(center + (from - center) * 0.08f, duration).SetEase(Ease.InCubic));
            s.Insert(delay, rt.DOLocalRotate(new Vector3(0f, 0f, 180f), duration, RotateMode.LocalAxisAdd));
            s.Insert(delay + duration * 0.7f, rt.DOScale(0.2f, duration * 0.3f).SetEase(Ease.InQuad));
            s.Insert(delay + duration * 0.75f, image.DOFade(0f, duration * 0.25f));
            s.OnComplete(() => image.gameObject.SetActive(false));
        }
    }

    private void LogoBurst()
    {
        PlaySound(soundLogo);

        Vector2 center = CloudCenter();

        if (confetti != null && cloud != null)
            confetti.BurstAt(cloud.position, logoConfetti, 90f, 160f, 0.75f);

        if (streakSprite == null || fxLayer == null) return;

        float offset = Random.Range(0f, 360f);

        for (int i = 0; i < streakCount; i++)
        {
            Image image = Get(streakSprite);
            RectTransform rt = image.rectTransform;

            float angle = offset + 360f / streakCount * i + Random.Range(-8f, 8f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float length = Random.Range(180f, 300f);
            float width = Random.Range(22f, 34f);
            float duration = Random.Range(0.38f, 0.5f);

            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, 10f);
            rt.anchoredPosition = center + dir * 120f;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            image.color = i % 3 == 0 ? new Color(0.8f, 1f, 0.75f, 1f) : Color.white;

            Sequence s = DOTween.Sequence().SetTarget(image).SetUpdate(true).SetLink(image.gameObject);

            s.Insert(0f, rt.DOAnchorPos(center + dir * Random.Range(520f, 640f), duration).SetEase(Ease.OutCubic));
            s.Insert(0f, rt.DOSizeDelta(new Vector2(width, length), duration * 0.35f).SetEase(Ease.OutQuad));
            s.Insert(duration * 0.35f, rt.DOSizeDelta(new Vector2(width * 0.5f, 20f), duration * 0.65f).SetEase(Ease.InQuad));
            s.Insert(duration * 0.5f, image.DOFade(0f, duration * 0.5f));
            s.OnComplete(() => image.gameObject.SetActive(false));
        }
    }

    private void TwinkleStars()
    {
        for (int i = 0; i < cloudStars.Length; i++)
        {
            RectTransform star = cloudStars[i];
            if (star == null) continue;

            float duration = 0.32f + (i % 4) * 0.07f;

            loops.Add(star.DOScale(0.55f, duration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true).SetLink(star.gameObject));
            loops.Add(star.DOLocalRotate(new Vector3(0f, 0f, i % 2 == 0 ? 25f : -25f), duration * 1.3f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(star.gameObject));
        }
    }

    private Vector2 CloudCenter()
    {
        if (cloud == null || fxLayer == null) return Vector2.zero;
        return fxLayer.InverseTransformPoint(cloud.position);
    }

    private void TickBarista(float dt)
    {
        if (clip == null || clip.Length == 0)
        {
            animating = false;
            return;
        }

        if (holdLeft > 0f)
        {
            holdLeft -= dt;
            if (holdLeft > 0f) return;

            frameTimer = 0f;
        }

        float step = HoldOf(clip, frame);

        frameTimer += dt;
        if (frameTimer < step - Refresh * 0.5f) return;

        frameTimer = Mathf.Min(frameTimer - step, step * 0.5f);
        frame++;

        if (frame >= clip.Length)
        {
            if (loopFrames != null && loopFrames.Length > 0) clip = loopFrames;

            frame = 0;
            holdLeft = loopPause;
        }

        ShowFrame(clip, frame);
    }

    private float HoldOf(Sprite[] frames, int index)
    {
        int[] holds = frames == cheerFrames ? cheerHolds : loopHolds;

        if (holds != null && index >= 0 && index < holds.Length && holds[index] > 0)
            return holds[index] * Refresh;

        return 1f / Mathf.Max(1f, fps);
    }

    private void ShowFrame(Sprite[] frames, int index)
    {
        if (barista == null || frames == null || frames.Length == 0) return;

        Sprite sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        if (sprite == null || barista.sprite == sprite) return;

        RectTransform target = barista.rectTransform;

        barista.sprite = sprite;
        target.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        target.sizeDelta = (Vector2)sprite.bounds.size * 100f * artScale;
    }

    private Image Get(Sprite sprite)
    {
        Image image = null;

        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf)
            {
                image = pool[i];
                break;
            }
        }

        if (image == null)
        {
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(fxLayer, false);

            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            pool.Add(image);
        }

        RectTransform rt = image.rectTransform;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        image.sprite = sprite;
        image.gameObject.SetActive(true);
        rt.SetAsLastSibling();

        return image;
    }

    private void KillLoops()
    {
        for (int i = 0; i < loops.Count; i++) loops[i]?.Kill();
        loops.Clear();

        ResetRotation(rays);
        ResetRotation(cloudStars);

        if (glints == null) return;

        for (int i = 0; i < glints.Length; i++)
        {
            if (glints[i] != null) glints[i].localScale = Vector3.zero;
        }
    }

    private static void ResetRotation(RectTransform[] targets)
    {
        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null) targets[i].localRotation = Quaternion.identity;
        }
    }

    private static void SetScale(Transform target, float scale)
    {
        if (target == null) return;

        target.DOKill();
        target.localScale = Vector3.one * scale;
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private static void PlaySound(string key)
    {
        if (!string.IsNullOrEmpty(key) && AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key);
    }
}
