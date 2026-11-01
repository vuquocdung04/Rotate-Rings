using UnityEngine;

namespace RotateRings
{
    /// <summary>
    /// Small star burst at a link collision point.
    /// Uses tex_star_005 (2x2 sheet of glowing stars): each particle picks one random star.
    /// </summary>
    public static class RingSparkFX
    {
        public static void Spawn(Vector2 position, Material material, int count = 7)
        {
            if (material == null) return;

            var go = new GameObject("RingSparkFX");
            go.transform.position = new Vector3(position.x, position.y, -1f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            // Radial burst in the 2D plane.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.3f;
            limit.dampen = 0.15f;

            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = material;
            psr.sortingOrder = 1001;

            ps.Play();
        }
    }
}
