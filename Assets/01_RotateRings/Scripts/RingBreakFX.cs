using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Bursts a piece into chunks laid out along its path, its arms (piece color) and its links (metal grey).
    /// Uses tex_sequence_011 (2x2 flipbook of rock chunks): each particle picks one random frame and is tinted.
    /// </summary>
    public static class RingBreakFX
    {
        public static readonly Color MetalGrey = new(0.86f, 0.88f, 0.92f); // silver; the chunk texture darkens it

        public static void Spawn(Piece ring)
        {
            if (ring.breakMaterial == null) return;

            var ps = Create("RingBreakFX", ring.transform.position, ring.breakMaterial);
            float t = ring.thickness;
            Vector2 center = ring.Center;

            // Body: along the path, pushed away from the piece center.
            float length = ring.shape.Length;
            int count = Mathf.Max(6, Mathf.RoundToInt(length / (t * 0.8f)));
            for (int i = 0; i < count; i++)
            {
                Vector2 local = ring.shape.Evaluate(length * (i + Random.value) / count, out _);
                Emit(ps, ring.transform.TransformPoint(local), center, t, ring.color);
            }

            // Arms go with the piece, same color.
            foreach (var arm in ring.Arms)
            {
                if (arm == null) continue;
                arm.GetSegment(out Vector2 a, out Vector2 b, out _);
                EmitAlong(ps, a, b, center, t, ring.color);
            }

            // Links (stem + sleeve) go with it too, as grey metal.
            foreach (var link in ring.Links)
            {
                if (link == null || link.Parent == null) continue;
                link.GetCapsule(out Vector2 a, out Vector2 b, out float stemPad, out float sleevePad);
                EmitAlong(ps, a, b, center, stemPad * 2f, MetalGrey);
                EmitBurst(ps, b, sleevePad, MetalGrey);
            }
        }

        /// <summary>Grey metal burst where a clamp lets go of its pipe.</summary>
        public static void SpawnClamp(PipeClamp clamp, Material material)
        {
            if (material == null) return;
            var ps = Create("ClampBreakFX", clamp.transform.position, material);
            EmitBurst(ps, clamp.transform.position, clamp.CollisionRadius, MetalGrey);
        }

        static ParticleSystem Create(string name, Vector3 position, Material material)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSpeed = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 512;

            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            // Random constant in [0,1) per particle = one fixed random frame of the 2x2 sheet.
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.9f), new Keyframe(1f, 0f)));

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.sortingOrder = 1000;

            ps.Play();
            return ps;
        }

        static void EmitAlong(ParticleSystem ps, Vector2 a, Vector2 b, Vector2 center, float width, Color color)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / (width * 0.8f)));
            for (int i = 0; i < count; i++)
                Emit(ps, Vector2.Lerp(a, b, (i + Random.value) / count), center, width, color);
        }

        /// <summary>A handful of chunks flying out of one point (sleeve, clamp).</summary>
        static void EmitBurst(ParticleSystem ps, Vector2 at, float radius, Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 from = at - Random.insideUnitCircle.normalized; // random outward direction
                Emit(ps, at + Random.insideUnitCircle * radius * 0.5f, from, radius, color);
            }
        }

        /// <param name="width">Thickness of what breaks; sets chunk size and spread.</param>
        static void Emit(ParticleSystem ps, Vector2 at, Vector2 center, float width, Color color)
        {
            Vector2 dir = at - center;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Random.insideUnitCircle.normalized;
            Vector2 pos = at + Random.insideUnitCircle * width * 0.35f;

            Color col = color * Random.Range(0.85f, 1.1f);
            col.a = 1f;
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = new Vector3(pos.x, pos.y, -1f),
                velocity = dir * Random.Range(0.8f, 2.6f) + Vector2.up * Random.Range(0.5f, 2f),
                startSize = Random.Range(width * 1.6f, width * 2.6f),
                startColor = col,
            };
            ps.Emit(ep, 1);
        }
    }
}
