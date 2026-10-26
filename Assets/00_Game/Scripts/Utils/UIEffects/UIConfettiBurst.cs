using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIConfettiBurst : MonoBehaviour
{
    [Serializable]
    public class Emitter
    {
        public RectTransform point;
        [Tooltip("Shoot direction in degrees, 90 = straight up.")]
        public float angle = 90f;
        public float spread = 30f;
        public int count = 24;
        public Vector2 speedRange = new Vector2(900f, 1500f);
    }

    private class Piece
    {
        public RectTransform rect;
        public Image image;
        public Vector2 pos;
        public Vector2 velocity;
        public float age;
        public float life;
        public float spin;
        public float angle;
        public float flutter;
        public float phase;
        public Color color;
    }

    [SerializeField] private List<Emitter> emitters = new List<Emitter>();
    [SerializeField] private Sprite[] sprites;
    [SerializeField] private Color[] colors =
    {
        new Color(1f, 0.36f, 0.42f), new Color(1f, 0.78f, 0.2f), new Color(0.35f, 0.8f, 1f),
        new Color(0.45f, 0.9f, 0.45f), new Color(0.75f, 0.5f, 1f), new Color(1f, 0.55f, 0.2f)
    };
    [SerializeField] private Vector2 sizeRange = new Vector2(18f, 32f);
    [SerializeField] private Vector2 lifeRange = new Vector2(1.6f, 2.4f);
    [SerializeField] private float gravity = 1400f;
    [Tooltip("Velocity kept per second; low values make pieces hang in the air like paper.")]
    [SerializeField, Range(0.01f, 1f)] private float drag = 0.12f;
    [SerializeField] private float maxFallSpeed = 420f;
    [SerializeField] private float sway = 60f;
    [SerializeField] private float spinSpeed = 540f;
    [Tooltip("Flip pieces like paper. Turn off for round droplets.")]
    [SerializeField] private bool tumble = true;
    [SerializeField] private int maxPieces = 120;

    private readonly List<Piece> active = new List<Piece>();
    private readonly Stack<Piece> pool = new Stack<Piece>();

    private RectTransform Area => (RectTransform)transform;

    public void Play()
    {
        for (int i = 0; i < emitters.Count; i++)
        {
            Emitter e = emitters[i];

            if (e == null)
                continue;

            Vector2 from = e.point != null ? (Vector2)Area.InverseTransformPoint(e.point.position) : Vector2.zero;
            Burst(from, e.count, e.angle, e.spread, e.speedRange);
        }
    }

    public void BurstAt(Vector3 worldPosition, int count, float angle = 90f, float spread = 70f, float speedScale = 0.6f)
    {
        Vector2 from = Area.InverseTransformPoint(worldPosition);
        Burst(from, count, angle, spread, new Vector2(900f, 1500f) * speedScale);
    }

    public void Clear()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            Release(active[i]);

        active.Clear();
    }

    private void Burst(Vector2 from, int count, float angle, float spread, Vector2 speedRange)
    {
        if (sprites == null || sprites.Length == 0)
            return;

        for (int i = 0; i < count && active.Count < maxPieces; i++)
        {
            float a = (angle + UnityEngine.Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(speedRange.x, speedRange.y);
            Piece p = Get();

            float size = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);
            Sprite sprite = sprites[UnityEngine.Random.Range(0, sprites.Length)];
            Vector2 native = sprite.rect.size;
            float k = size / Mathf.Max(native.x, native.y);

            p.image.sprite = sprite;
            p.rect.sizeDelta = native * k;
            p.pos = from;
            p.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            p.age = 0f;
            p.life = UnityEngine.Random.Range(lifeRange.x, lifeRange.y);
            p.spin = UnityEngine.Random.Range(-spinSpeed, spinSpeed);
            p.angle = UnityEngine.Random.Range(0f, 360f);
            p.flutter = UnityEngine.Random.Range(4f, 9f);
            p.phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            p.color = colors.Length > 0 ? colors[UnityEngine.Random.Range(0, colors.Length)] : Color.white;
            p.image.color = p.color;
            p.rect.SetAsLastSibling();

            active.Add(p);
        }
    }

    private void Update()
    {
        if (active.Count == 0)
            return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float keep = Mathf.Pow(drag, dt);

        for (int i = active.Count - 1; i >= 0; i--)
        {
            Piece p = active[i];
            p.age += dt;

            if (p.age >= p.life)
            {
                Release(p);
                active.RemoveAt(i);
                continue;
            }

            p.velocity *= keep;
            p.velocity.y = Mathf.Max(p.velocity.y - gravity * dt, -maxFallSpeed);
            p.pos += p.velocity * dt;
            p.angle += p.spin * dt;

            float t = p.age / p.life;
            float swayX = Mathf.Sin(p.age * 3f + p.phase) * sway * Mathf.Clamp01(p.age * 2f);
            float pop = Mathf.Clamp01(p.age * 12f);

            p.rect.anchoredPosition = p.pos + new Vector2(swayX, 0f);
            p.rect.localRotation = Quaternion.Euler(0f, 0f, p.angle);
            float flip = tumble ? Mathf.Cos(p.age * p.flutter + p.phase) : 1f;
            p.rect.localScale = new Vector3(flip * pop, pop, 1f);

            Color c = p.color;
            c.a = 1f - Mathf.InverseLerp(0.75f, 1f, t);
            p.image.color = c;
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private Piece Get()
    {
        Piece p = pool.Count > 0 ? pool.Pop() : Create();
        p.rect.gameObject.SetActive(true);
        return p;
    }

    private Piece Create()
    {
        var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);

        var image = go.GetComponent<Image>();
        image.raycastTarget = false;

        return new Piece { rect = (RectTransform)go.transform, image = image };
    }

    private void Release(Piece p)
    {
        if (p.rect != null)
            p.rect.gameObject.SetActive(false);

        pool.Push(p);
    }
}
