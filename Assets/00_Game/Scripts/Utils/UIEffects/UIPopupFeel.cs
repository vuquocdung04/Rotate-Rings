using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UIPopupFeel : MonoBehaviour
{
    public enum Motion
    {
        Pop,
        Drop,
        Rise,
        SlideLeft,
        SlideRight,
        Fade
    }

    [Serializable]
    public class Step
    {
        public Transform target;
        public Motion motion = Motion.Pop;
        public float delay;
        public float duration = 0.35f;
    }

    [Header("Entrance")]
    [SerializeField] private List<Step> steps = new();
    [SerializeField] private float distance = 140f;
    [SerializeField] private string openSound;

    [Header("List")]
    [SerializeField] private Transform list;
    [SerializeField] private float listDelay = 0.2f;
    [SerializeField] private float listStagger = 0.05f;
    [SerializeField] private int listCap = 10;

    [Header("Idle")]
    [SerializeField] private List<Transform> breathe = new();
    [SerializeField] private float breatheScale = 1.05f;
    [SerializeField] private List<Transform> wiggle = new();
    [SerializeField] private float wiggleInterval = 2.2f;
    [SerializeField] private List<Transform> bob = new();
    [SerializeField] private float bobHeight = 10f;
    [SerializeField] private List<Transform> spin = new();
    [SerializeField] private float spinDuration = 12f;

    private struct Pose
    {
        public Transform t;
        public RectTransform rect;
        public Vector3 pos;
        public bool moved;
        public Vector3 scale;
        public Quaternion rot;
        public CanvasGroup group;
        public float alpha;
    }

    private readonly Dictionary<Transform, Pose> poses = new();
    private readonly List<Tween> tweens = new();
    private Coroutine routine;

    public void Play()
    {
        Stop();
        HideSteps();
        routine = StartCoroutine(PlayNextFrame());
    }

    public void Stop()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        for (int i = 0; i < tweens.Count; i++) tweens[i]?.Kill();
        tweens.Clear();

        foreach (Pose p in poses.Values)
        {
            if (p.t == null) continue;

            if (p.moved) SetPos(p, p.pos);

            p.t.localScale = p.scale;
            p.t.localRotation = p.rot;

            if (p.group != null) p.group.alpha = p.alpha;
        }

        poses.Clear();
    }

    private void OnDisable() => Stop();

    private static Vector3 GetPos(Transform t) => t is RectTransform r ? (Vector3)r.anchoredPosition : t.localPosition;

    private static void SetPos(Pose p, Vector3 value)
    {
        if (p.rect != null) p.rect.anchoredPosition = value;
        else p.t.localPosition = value;
    }

    private static Tween MoveTo(Pose p, Vector3 value, float duration)
        => p.rect != null ? p.rect.DOAnchorPos(value, duration) : p.t.DOLocalMove(value, duration);

    private void MarkMoved(Transform t)
    {
        Pose p = poses[t];
        p.moved = true;
        poses[t] = p;
    }

    private Pose Cache(Transform t, bool needGroup = false)
    {
        if (poses.TryGetValue(t, out Pose pose))
        {
            if (needGroup && pose.group == null)
            {
                pose.group = GetGroup(t);
                pose.alpha = pose.group.alpha;
                poses[t] = pose;
            }

            return pose;
        }

        pose = new Pose { t = t, rect = t as RectTransform, pos = GetPos(t), scale = t.localScale, rot = t.localRotation };

        if (needGroup)
        {
            pose.group = GetGroup(t);
            pose.alpha = pose.group.alpha;
        }

        poses[t] = pose;
        return pose;
    }

    private static CanvasGroup GetGroup(Transform t)
    {
        CanvasGroup group = t.GetComponent<CanvasGroup>();
        return group != null ? group : t.gameObject.AddComponent<CanvasGroup>();
    }

    private void HideSteps()
    {
        for (int i = 0; i < steps.Count; i++)
        {
            Step s = steps[i];
            if (s?.target == null) continue;

            Pose p = Cache(s.target, true);
            p.group.alpha = 0f;
        }
    }

    private IEnumerator PlayNextFrame()
    {
        yield return null;

        routine = null;

        if (!string.IsNullOrEmpty(openSound) && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(openSound);

        float end = 0f;

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i]?.target != null) end = Mathf.Max(end, PlayStep(steps[i]));
        }

        end = Mathf.Max(end, PlayList());

        tweens.Add(DOVirtual.DelayedCall(end, StartIdle).SetUpdate(true).SetLink(gameObject));
    }

    private float PlayStep(Step s)
    {
        Transform t = s.target;
        Pose p = Cache(t, true);
        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(t.gameObject).SetDelay(s.delay);

        if (s.motion == Motion.Pop)
        {
            t.localScale = Vector3.zero;
            p.group.alpha = p.alpha;
            seq.Append(t.DOScale(p.scale, s.duration).SetEase(Ease.OutBack, 2f));
        }
        else
        {
            Vector3 offset = s.motion switch
            {
                Motion.Drop => new Vector3(0f, distance, 0f),
                Motion.Rise => new Vector3(0f, -distance, 0f),
                Motion.SlideLeft => new Vector3(distance, 0f, 0f),
                Motion.SlideRight => new Vector3(-distance, 0f, 0f),
                _ => Vector3.zero
            };

            p.group.alpha = 0f;
            MarkMoved(t);
            SetPos(p, p.pos + offset);
            seq.Append(MoveTo(p, p.pos, s.duration).SetEase(Ease.OutBack, 1.3f));
            seq.Join(p.group.DOFade(p.alpha, s.duration * 0.6f));
        }

        tweens.Add(seq);
        return s.delay + s.duration;
    }

    private float PlayList()
    {
        if (list == null) return 0f;

        float end = 0f;
        int order = 0;

        for (int i = 0; i < list.childCount; i++)
        {
            Transform row = list.GetChild(i);
            if (!row.gameObject.activeInHierarchy) continue;

            Pose p = Cache(row, true);
            float delay = listDelay + Mathf.Min(order, listCap) * listStagger;

            p.group.alpha = 0f;
            row.localScale = p.scale * 0.85f;

            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(row.gameObject).SetDelay(delay);
            seq.Append(row.DOScale(p.scale, 0.3f).SetEase(Ease.OutBack, 2f));
            seq.Join(p.group.DOFade(p.alpha, 0.2f));
            tweens.Add(seq);

            end = Mathf.Max(end, delay + 0.3f);
            order++;
        }

        return end;
    }

    private void StartIdle()
    {
        for (int i = 0; i < breathe.Count; i++)
        {
            Transform t = breathe[i];
            if (t == null) continue;

            Pose p = Cache(t);
            tweens.Add(t.DOScale(p.scale * breatheScale, 0.65f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(t.gameObject));
        }

        for (int i = 0; i < wiggle.Count; i++)
        {
            Transform t = wiggle[i];
            if (t == null) continue;

            Cache(t);
            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(t.gameObject);
            seq.AppendInterval(wiggleInterval * (0.4f + 0.15f * i));
            seq.Append(t.DOPunchRotation(new Vector3(0f, 0f, 9f), 0.5f, 8, 0.6f));
            seq.AppendInterval(wiggleInterval * 0.6f);
            seq.SetLoops(-1, LoopType.Restart);
            tweens.Add(seq);
        }

        for (int i = 0; i < bob.Count; i++)
        {
            Transform t = bob[i];
            if (t == null) continue;

            Pose p = Cache(t);
            MarkMoved(t);
            tweens.Add(MoveTo(p, p.pos + new Vector3(0f, bobHeight, 0f), 0.9f + 0.15f * i)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(t.gameObject));
        }

        for (int i = 0; i < spin.Count; i++)
        {
            Transform t = spin[i];
            if (t == null) continue;

            Cache(t);
            tweens.Add(t.DOLocalRotate(new Vector3(0f, 0f, i % 2 == 0 ? -360f : 360f), spinDuration, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear).SetLoops(-1).SetUpdate(true).SetLink(t.gameObject));
        }
    }
}
