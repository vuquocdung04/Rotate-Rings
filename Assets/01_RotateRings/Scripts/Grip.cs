using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Something that holds a piece at a point its path must pass through:
    /// a link sleeve owned by another piece, or a world-fixed pipe clamp.
    /// The held piece can only move along itself through the grip; the grip lets go once the piece slides off.
    /// </summary>
    public abstract class Grip : MonoBehaviour
    {
        public Piece child;

        protected bool attached = true;

        public Piece Child => attached ? child : null;
        public abstract Vector2 GripWorldPos { get; }

        protected void RegisterWithChild()
        {
            if (Application.isPlaying && attached && child != null && !child.Grips.Contains(this))
                child.Grips.Add(this);
        }

        protected void UnregisterFromChild()
        {
            if (child != null) child.Grips.Remove(this);
        }

        public void Release()
        {
            if (!attached) return;
            attached = false;
            var c = child;
            if (c != null) c.Grips.Remove(this);
            OnReleased();
            if (c != null) c.TryBreak();
        }

        protected virtual void OnReleased() { }
    }
}
