using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Bursts a ring into chunks laid out along its arc.
    /// Uses tex_sequence_011 (2x2 flipbook of rock chunks): each particle picks one random frame and is tinted with the ring color.
    /// </summary>
    public static class RingBreakFX
    {
        public static void Spawn(Ring ring)
        {
            if (ring.breakMaterial == null) return;

            var go = new GameObject("RingBreakFX");
            go.transform.position = ring.transform.position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float t = ring.thickness;

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(t * 1.6f, t * 2.6f);
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
            psr.sharedMaterial = ring.breakMaterial;
            psr.sortingOrder = 1000;

            ps.Play();

            // Emit along the body arc (skip the gap).
            float gapHalf = ring.gapAngle * 0.5f;
            float from = ring.gapCenter + gapHalf;
            float to = ring.gapCenter + 360f - gapHalf;
            float arcLen = (to - from) * Mathf.Deg2Rad * ring.radius;
            int count = Mathf.Max(6, Mathf.RoundToInt(arcLen / (t * 0.8f)));

            Vector2 center = ring.Center;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = (Mathf.Lerp(from, to, (i + Random.value) / count) + ring.Angle) * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = center + dir * ring.radius + Random.insideUnitCircle * t * 0.35f;

                ep.position = new Vector3(pos.x, pos.y, -1f);
                ep.velocity = (Vector3)(dir * Random.Range(0.8f, 2.6f) + Vector2.up * Random.Range(0.5f, 2f));
                Color col = ring.color * Random.Range(0.85f, 1.1f);
                col.a = 1f;
                ep.startColor = col;
                ps.Emit(ep, 1);
            }
        }
    }
}
