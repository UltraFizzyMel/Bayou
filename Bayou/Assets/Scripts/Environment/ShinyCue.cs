using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>Small looping sparkle so a world pickup reads as collectible.</summary>
    public static class ShinyCue
    {
        public static void Ensure(Transform host, Color color)
        {
            if (host == null) return;
            if (host.Find("Sparkle") != null) return;

            var go = new GameObject("Sparkle");
            go.transform.SetParent(host, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = color;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            ps.Play();
        }
    }
}
