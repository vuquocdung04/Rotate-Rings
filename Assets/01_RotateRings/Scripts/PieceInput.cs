using UnityEngine;
using UnityEngine.InputSystem;

namespace RotateRings
{
    /// <summary>
    /// Press on a piece (or inside a ring) and drag: rings rotate with the pointer's angle around their center,
    /// pipes follow the pointer projected on their slide axis.
    /// </summary>
    public class PieceInput : MonoBehaviour
    {
        public Camera cam;
        [Tooltip("Touching within this many thicknesses of a body picks that piece over ring-interior hits.")]
        public float bodyPickScale = 1.5f;

        Piece active;
        float lastAngle;
        Vector2 lastPoint;

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
                    lastPoint = wp;
                }
            }
            else if (active != null && pointer.press.isPressed)
            {
                if (active.Motion == MotionKind.Slide)
                {
                    active.Drag(Vector2.Dot(wp - lastPoint, active.SlideAxis));
                    lastPoint = wp;
                }
                else
                {
                    float a = AngleAround(active, wp);
                    float delta = Mathf.DeltaAngle(lastAngle, a);
                    lastAngle = a;
                    active.Drag(delta);
                }
            }

            if (active != null && pointer.press.wasReleasedThisFrame)
            {
                active.EndDrag();
                active = null;
            }
        }

        static float AngleAround(Piece p, Vector2 wp)
        {
            Vector2 d = wp - p.Center;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        }

        Piece Pick(Vector2 wp)
        {
            Piece best = null;
            float bestScore = float.MaxValue;
            foreach (var p in Piece.All)
            {
                if (p == null || p.IsBroken) continue;
                if (!p.TryPick(wp, bodyPickScale, out bool onBody, out float interiorScore)) continue;

                // Body hits beat interior hits; among body hits, the piece drawn on top wins.
                float score = onBody ? -10000f - p.sortingOrder : interiorScore;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
            return best;
        }
    }
}
