using UnityEngine;
using UnityEngine.InputSystem;

namespace RotateRings
{
    /// <summary>Press on/inside a ring, then drag around its center to rotate it.</summary>
    public class RingInput : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Touching within this many thicknesses of a ring body picks that ring over interior hits.")]
        public float bodyPickScale = 1.5f;

        Ring active;
        float lastAngle;

        void Awake()
        {
            if (cam == null) cam = Camera.main;
        }

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || cam == null) return;

            Vector2 sp = pointer.position.ReadValue();
            Vector2 wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));

            if (active != null && active.IsBroken) active = null;

            if (pointer.press.wasPressedThisFrame)
            {
                active = Pick(wp);
                if (active != null)
                {
                    active.BeginDrag();
                    lastAngle = AngleAround(active, wp);
                }
            }
            else if (active != null && pointer.press.isPressed)
            {
                float a = AngleAround(active, wp);
                float delta = Mathf.DeltaAngle(lastAngle, a);
                lastAngle = a;
                active.Drag(delta);
            }

            if (active != null && pointer.press.wasReleasedThisFrame)
            {
                active.EndDrag();
                active = null;
            }
        }

        static float AngleAround(Ring r, Vector2 wp)
        {
            Vector2 d = wp - r.Center;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        Ring Pick(Vector2 wp)
        {
            Ring best = null;
            float bestScore = float.MaxValue;
            foreach (var r in Ring.All)
            {
                if (r == null || r.IsBroken) continue;
                float dist = Vector2.Distance(wp, r.Center);
                if (dist > r.radius + r.thickness) continue;

                // Body hits beat interior hits; among body hits, the ring drawn on top wins.
                bool onBody = Mathf.Abs(dist - r.radius) <= r.thickness * bodyPickScale;
                float score = onBody ? -10000f - r.sortingOrder : dist / r.radius;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = r;
                }
            }
            return best;
        }
    }
}
