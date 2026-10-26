using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIFireworks : MonoBehaviour
{
    private class Spark
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos;
        public Vector2 velocity;
        public float age;
        public float life;
        public float size;
        public float phase;
        public Color color;
        public Kind kind;
        public Vector2 target;
    }

    private enum Kind { Trail, Glitter, Flash, Shell }

    [Header("Art")]
    [Tooltip("Streak with a bright head at the top (points along +Y).")]
    [SerializeField] private Sprite trailSprite;
    [Tooltip("Small soft dot for the flash.")]
    [SerializeField] private Sprite glowSprite;
    [Tooltip("Four-point twinkle left behind by a burst.")]
    [SerializeField] private Sprite glitterSprite;

    [Header("Burst")]
    [SerializeField] private Color[] colors =
    {
        new Color(1f, 0.82f, 0.3f), new Color(1f, 0.45f, 0.55f), new Color(0.45f, 0.9f, 0.7f),
        new Color(0.45f, 0.75f, 1f), new Color(1f, 0.6f, 0.25f), new Color(0.8f, 0.6f, 1f)
    };
    [SerializeField] private Vector2Int sparkCount = new Vector2Int(22, 30);
    [SerializeField] private Vector2 speedRange = new Vector2(420f, 620f);
    [SerializeField] private Vector2 lifeRange = new Vector2(0.9f, 1.3f);
    [SerializeField] private float sparkWidth = 16f;
    [Tooltip("Trail length per unit of speed, clamped below.")]
    [SerializeField] private float trailPerSpeed = 0.16f;
    [SerializeField] private Vector2 trailLength = new Vector2(18f, 96f);
    [SerializeField] private float gravity = 260f;
    [Tooltip("Velocity kept per second.")]
    [SerializeField, Range(0.01f, 1f)] private float drag = 0.08f;
    [SerializeField] private int glitterPerBurst = 8;
    [SerializeField] private float flashSize = 260f;

    [Header("Shell")]
    [SerializeField] private float shellDuration = 0.38f;
    [SerializeField] private float shellRise = 520f;

    [Header("Show")]
    [Tooltip("Burst points across the area (0..1), used in turn by Play.")]
    [SerializeField] private Vector2[] points =
    {
        new Vector2(0.24f, 0.72f), new Vector2(0.78f, 0.78f), new Vector2(0.5f, 0.86f),
        new Vector2(0.16f, 0.52f), new Vector2(0.84f, 0.56f)
    };
    [Tooltip("Seconds between the first bursts of a show.")]
    [SerializeField] private float[] openingDelays = { 0f, 0.28f, 0.55f, 0.9f };
    [Tooltip("Gap between later bursts while the show keeps going.")]
    [SerializeField] private Vector2 loopGap = new Vector2(0.9f, 1.5f);
    [SerializeField] private int maxSparks = 220;

    private readonly List<Spark> active = new List<Spark>();
    private readonly Stack<Spark> pool = new Stack<Spark>();

    private bool showing;
    private bool looping;
    private int showStep;
    private float nextBurstAt;
    private float clock;

    private RectTransform Area => (RectTransform)transform;

    public void Play(bool loop = true)
    {
        showing = true;
        looping = loop;
        showStep = 0;
        clock = 0f;
        nextBurstAt = 0f;
    }

    public void Stop()
    {
        showing = false;
    }

    public void Clear()
    {
        showing = false;

        for (int i = active.Count - 1; i >= 0; i--)
            Release(active[i]);

        active.Clear();
    }

    public void BurstAt(Vector2 local, Color color)
    {
        Flash(local, color);

        int count = Random.Range(sparkCount.x, sparkCount.y + 1);
        float step = 360f / count;
        float offset = Random.Range(0f, 360f);
        float speedScale = Random.Range(0.85f, 1.1f);

        for (int i = 0; i < count && active.Count < maxSparks; i++)
        {
            float angle = (offset + step * i + Random.Range(-0.3f, 0.3f) * step) * Mathf.Deg2Rad;
            float speed = Random.Range(speedRange.x, speedRange.y) * speedScale;
            Spark s = Get(Kind.Trail, trailSprite);

            s.pos = local;
            s.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            s.life = Random.Range(lifeRange.x, lifeRange.y);
            s.size = sparkWidth * Random.Range(0.8f, 1.15f);
            s.color = Color.Lerp(color, Color.white, Random.Range(0f, 0.25f));
        }

        for (int i = 0; i < glitterPerBurst && active.Count < maxSparks; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Spark s = Get(Kind.Glitter, glitterSprite != null ? glitterSprite : glowSprite);

            s.pos = local;
            s.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(80f, 260f);
            s.life = Random.Range(1.1f, 1.6f);
            s.size = Random.Range(22f, 40f);
            s.color = Color.Lerp(color, Color.white, 0.55f);
        }
    }

    public void LaunchAt(Vector2 local, Color color)
    {
        if (trailSprite == null || active.Count >= maxSparks)
        {
            BurstAt(local, color);
            return;
        }

        Spark s = Get(Kind.Shell, trailSprite);

        s.target = local;
        s.pos = local + new Vector2(Random.Range(-60f, 60f), -shellRise);
        s.velocity = (s.target - s.pos) / Mathf.Max(0.05f, shellDuration);
        s.life = shellDuration;
        s.size = sparkWidth * 0.8f;
        s.color = Color.Lerp(color, Color.white, 0.5f);
    }

    private void Flash(Vector2 local, Color color)
    {
        if (glowSprite == null || active.Count >= maxSparks)
            return;

        Spark s = Get(Kind.Flash, glowSprite);

        s.pos = local;
        s.velocity = Vector2.zero;
        s.life = 0.35f;
        s.size = flashSize;
        s.color = Color.Lerp(color, Color.white, 0.6f);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        if (showing)
            TickShow(dt);

        if (active.Count == 0)
            return;

        float keep = Mathf.Pow(drag, dt);

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Spark s = active[i];
            s.age += dt;

            if (s.age >= s.life)
            {
                if (s.kind == Kind.Shell)
                    BurstAt(s.target, s.color);

                Release(s);
                active.RemoveAt(i);
                continue;
            }

            Apply(s, dt, keep);
        }
    }

    private void TickShow(float dt)
    {
        clock += dt;

        if (clock < nextBurstAt)
            return;

        bool opening = showStep < openingDelays.Length;
        Vector2 point = PointFor(showStep);
        Color color = colors.Length > 0 ? colors[showStep % colors.Length] : Color.white;

        if (opening)
            BurstAt(point, color);
        else
            LaunchAt(point, color);

        showStep++;

        if (showStep < openingDelays.Length)
        {
            nextBurstAt = openingDelays[showStep];
            return;
        }

        if (!looping)
        {
            showing = false;
            return;
        }

        nextBurstAt = clock + Random.Range(loopGap.x, loopGap.y);
    }

    private Vector2 PointFor(int step)
    {
        Rect rect = Area.rect;
        Vector2 n = points != null && points.Length > 0
            ? points[step % points.Length]
            : new Vector2(0.5f, 0.75f);

        if (step >= points.Length)
            n += new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(-0.06f, 0.06f));

        return new Vector2(rect.xMin + rect.width * n.x, rect.yMin + rect.height * n.y);
    }

    private void Apply(Spark s, float dt, float keep)
    {
        float t = s.age / s.life;

        switch (s.kind)
        {
            case Kind.Flash:
            {
                float k = 1f - (1f - t) * (1f - t);
                s.rect.anchoredPosition = s.pos;
                s.rect.sizeDelta = Vector2.one * s.size * Mathf.Lerp(0.3f, 1f, k);
                SetColor(s, (1f - t) * 0.85f);
                return;
            }
            case Kind.Shell:
            {
                s.pos += s.velocity * dt;
                Orient(s, s.velocity, Mathf.Clamp(s.velocity.magnitude * 0.12f, trailLength.x, trailLength.y * 1.2f));
                SetColor(s, Mathf.Clamp01(t * 4f));
                return;
            }
            case Kind.Glitter:
            {
                s.velocity *= keep;
                s.velocity.y -= gravity * 0.35f * dt;
                s.pos += s.velocity * dt;
                s.rect.anchoredPosition = s.pos;

                float pop = Mathf.Clamp01(s.age * 8f);
                float twinkle = 0.55f + 0.45f * Mathf.Sin(s.age * 26f + s.phase);
                s.rect.sizeDelta = Vector2.one * s.size * pop * (0.8f + 0.3f * twinkle);
                s.rect.localRotation = Quaternion.Euler(0f, 0f, s.phase * 57f + s.age * 90f);
                SetColor(s, twinkle * (1f - Mathf.InverseLerp(0.6f, 1f, t)));
                return;
            }
            default:
            {
                s.velocity *= keep;
                s.velocity.y -= gravity * dt;
                s.pos += s.velocity * dt;

                float speed = s.velocity.magnitude;
                Orient(s, s.velocity, Mathf.Clamp(speed * trailPerSpeed, trailLength.x, trailLength.y));

                float fade = 1f - Mathf.InverseLerp(0.55f, 1f, t);
                float flicker = t > 0.45f ? 0.6f + 0.4f * Mathf.Sin(s.age * 38f + s.phase) : 1f;
                SetColor(s, fade * flicker);
                return;
            }
        }
    }

    private void Orient(Spark s, Vector2 velocity, float length)
    {
        s.rect.anchoredPosition = s.pos;
        s.rect.sizeDelta = new Vector2(s.size, Mathf.Max(s.size, length));

        if (velocity.sqrMagnitude > 0.01f)
            s.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
    }

    private static void SetColor(Spark s, float alpha)
    {
        Color c = s.color;
        c.a = Mathf.Clamp01(alpha);
        s.image.color = c;
    }

    private void OnDisable()
    {
        Clear();
    }

    private Spark Get(Kind kind, Sprite sprite)
    {
        Spark s = pool.Count > 0 ? pool.Pop() : Create();

        s.kind = kind;
        s.age = 0f;
        s.phase = Random.Range(0f, Mathf.PI * 2f);
        s.image.sprite = sprite;
        s.image.color = new Color(1f, 1f, 1f, 0f);
        s.rect.pivot = kind == Kind.Trail || kind == Kind.Shell ? new Vector2(0.5f, 0.875f) : new Vector2(0.5f, 0.5f);
        s.rect.localRotation = Quaternion.identity;
        s.rect.localScale = Vector3.one;
        s.rect.gameObject.SetActive(true);
        s.rect.SetAsLastSibling();

        active.Add(s);

        return s;
    }

    private Spark Create()
    {
        var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;

        return new Spark { rect = rect, image = image };
    }

    private void Release(Spark s)
    {
        if (s.rect != null)
            s.rect.gameObject.SetActive(false);

        pool.Push(s);
    }
}
