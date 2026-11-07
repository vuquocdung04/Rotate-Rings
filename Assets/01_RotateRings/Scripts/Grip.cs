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
        /// <summary>Half the grip's length along the held piece: all of it must sit on a straight part to slide.</summary>
        public abstract float HalfLength { get; }
        /// <summary>Extra distance past the piece's edge before it lets go: the grip's own half width, so its far edge is clear.</summary>
        public virtual float ReleaseClearance => 0f;

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
