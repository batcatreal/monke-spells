using UnityEngine;
using UnityEngine.Rendering;

namespace GTAGWizard.Visuals
{
    /// <summary>
    /// Declarative description of one particle system. Callers fill in only what they need.
    /// Rates/counts should already be multiplied by the user's ParticleIntensity.
    /// </summary>
    internal sealed class ParticleSpec
    {
        public float LifeMin = 0.5f;
        public float LifeMax = 1f;
        public float SpeedMin = 0f;
        public float SpeedMax = 0f;
        public float SizeMin = 0.05f;
        public float SizeMax = 0.1f;

        /// <summary>Particles per second. Use 0 for burst-only systems (driven by ParticleSystem.Emit).</summary>
        public float Rate = 10f;
        public int MaxParticles = 64;

        public Color StartColor = Color.white;
        public Gradient ColorOverLifetime;          // null = no colour animation
        public AnimationCurve SizeOverLifetime;     // null = constant size

        public float Gravity = 0f;                  // negative = floats upward
        public Vector3 Drift = Vector3.zero;        // extra world-space velocity
        public float NoiseStrength = 0f;            // >0 adds a swirly wobble
        public float NoiseFrequency = 1f;

        /// <summary>Emitter shape; null = all particles spawn at the system's origin.</summary>
        public ParticleSystemShapeType? Shape = ParticleSystemShapeType.Sphere;
        public float ShapeRadius = 0.05f;

        /// <summary>World = particles stay where they were spawned (trails); Local = they move with the object.</summary>
        public bool WorldSpace = false;
        public ParticleSystemScalingMode Scaling = ParticleSystemScalingMode.Local;

        public bool Stretched = false;              // streak-style sparks
        public bool RandomRotation = true;
    }

    internal static class ParticleFactory
    {
        /// <summary>
        /// Builds a ParticleSystem as a child of <paramref name="parent"/>.
        /// IMPORTANT: call this while the parent hierarchy is INACTIVE so the system is fully configured
        /// before it first plays. Returns null if no particle shader is available.
        /// </summary>
        public static ParticleSystem Create(VisualKit kit, Transform parent, string name, ParticleSpec spec)
        {
            if (kit == null || !kit.CanRenderParticles) return null;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.loop = true; // looping + rate 0 keeps burst systems alive so Emit() always works
            main.playOnAwake = true;
            main.maxParticles = Mathf.Max(8, spec.MaxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.LifeMin, spec.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.SpeedMin, spec.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.SizeMin, spec.SizeMax);
            main.startColor = spec.StartColor;
            main.gravityModifier = spec.Gravity;
            main.simulationSpace = spec.WorldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = spec.Scaling;
            if (spec.RandomRotation)
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = spec.Rate;

            ParticleSystem.ShapeModule shape = system.shape;
            if (spec.Shape.HasValue)
            {
                shape.enabled = true;
                shape.shapeType = spec.Shape.Value;
                shape.radius = spec.ShapeRadius;
            }
            else
            {
                shape.enabled = false;
            }

            if (spec.ColorOverLifetime != null)
            {
                ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
                colour.enabled = true;
                colour.color = new ParticleSystem.MinMaxGradient(spec.ColorOverLifetime);
            }

            if (spec.SizeOverLifetime != null)
            {
                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, spec.SizeOverLifetime);
            }

            if (spec.Drift != Vector3.zero)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                // All three axes must use the same curve mode, so always set all three.
                velocity.x = spec.Drift.x;
                velocity.y = spec.Drift.y;
                velocity.z = spec.Drift.z;
            }

            if (spec.NoiseStrength > 0f)
            {
                ParticleSystem.NoiseModule noise = system.noise;
                noise.enabled = true;
                noise.strength = spec.NoiseStrength;
                noise.frequency = spec.NoiseFrequency;
                noise.quality = ParticleSystemNoiseQuality.Low; // cheapest setting
            }

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = kit.AdditiveSoft;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (spec.Stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2f;
                renderer.velocityScale = 0.1f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            return system;
        }

        /// <summary>Applies a layer to a GameObject and all its children.</summary>
        public static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i).gameObject, layer);
        }
    }

    /// <summary>
    /// Lets an animation grow/shrink a particle system by rewriting its start size each frame
    /// (new particles take the new size; short lifetimes make this look like a smooth scale).
    /// </summary>
    internal sealed class ScaledParticles
    {
        private readonly ParticleSystem _system;
        private readonly float _min;
        private readonly float _max;

        public ScaledParticles(ParticleSystem system, float min, float max)
        {
            _system = system;
            _min = min;
            _max = max;
        }

        public void Apply(float scale)
        {
            if (_system == null) return;
            ParticleSystem.MainModule main = _system.main;
            main.startSize = new ParticleSystem.MinMaxCurve(_min * scale, _max * scale);
        }
    }
}
