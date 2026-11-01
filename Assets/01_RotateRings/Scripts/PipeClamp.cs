using System.Collections.Generic;
using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// World-fixed metal clamp (clamp04). The pipe it holds can only slide along the straight segment inside it;
    /// a bend can't pass through, sliding off an end frees the pipe.
    /// Other pieces collide with it as a circle.
    /// </summary>
    [ExecuteAlways]
    public class PipeClamp : Grip
    {
        public static readonly List<PipeClamp> All = new();

        [Tooltip("Sprite size (x along the pipe). The clamp04 body is ~65% of the sprite height, so keep y around 2x the pipe thickness to wrap it.")]
        public Vector2 size = new(0.56f, 0.72f);
        public SpriteRenderer sprite;

        public override Vector2 GripWorldPos => transform.position;
        public float CollisionRadius => Mathf.Max(size.x, size.y) * 0.5f;

        void OnEnable()
        {
            All.Add(this);
            RegisterWithChild();
            Layout();
        }

        void OnDisable()
        {
            All.Remove(this);
            UnregisterFromChild();
        }

        void Update()
        {
            if (!Application.isPlaying) Layout();
        }

        // The clamp goes away with the pipe it held.
        protected override void OnReleased()
        {
            if (Application.isPlaying) Destroy(gameObject);
        }

        /// <summary>Snaps onto the pipe's centerline and turns to the segment direction (flanges across the pipe).</summary>
        public void Layout()
        {
            if (child == null || sprite == null || child.shape == null || child.shape.segments.Count == 0) return;

            Vector2 local = child.transform.InverseTransformPoint(transform.position);
            int index = child.shape.ClosestSegmentIndex(local, out Vector2 onPath);
            var seg = child.shape.segments[index];
            Vector2 tangent = child.transform.TransformDirection(seg.Tangent(0.5f));

            Vector3 snapped = child.transform.TransformPoint(onPath);
            transform.position = new Vector3(snapped.x, snapped.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);

            if (sprite.sprite != null)
            {
                Vector2 s = sprite.sprite.bounds.size;
                sprite.transform.localScale = new Vector3(size.x / s.x, size.y / s.y, 1f);
            }
            sprite.sortingOrder = child.sortingOrder + 2;
        }
    }
}
