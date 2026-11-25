using System.Collections.Generic;
using BepInEx.Logging;
using GTAGWizard.Config;
using GTAGWizard.Controls;
using GTAGWizard.Game;
using UnityEngine;

namespace GTAGWizard.Core
{
    /// <summary>
    /// Local-only cosmetic controller. It reads XR input and creates objects without colliders,
    /// rigidbodies, networking, damage, or interaction with other players/world objects.
    /// </summary>
    internal sealed class WizardController : MonoBehaviour
    {
        private sealed class TimedEffect
        {
            public GameObject Root;
            public float Started;
            public float Duration;
            public Vector3 Start;
            public Vector3 End;
            public bool IsFireball;
            public LineRenderer Line;
        }

        private ManualLogSource _log;
        private WizardSettings _settings;
        private readonly WizardInput _input = new WizardInput();
        private readonly List<TimedEffect> _effects = new List<TimedEffect>();
        private Transform _hand;
        private GameObject _staff;
        private Material _wood, _metal, _crystal, _fire, _bolt;
        private float _nextFire, _nextBolt;
        private bool _initialized;

        public void Initialize(ManualLogSource log, WizardSettings settings)
        {
            _log = log;
            _settings = settings;          
            GameBridge.Log = log;
            CreateMaterials();
            _initialized = true;
            _log.LogInfo("WizardController initialized. Controls: right Grip=staff, A=fireball, Trigger=lightning; B unused.");
        }

        private void Update()
        {
            if (!_initialized) return;

            _input.Poll();
            _hand = GameBridge.GetRightHand();

            if (_hand != null)
            {
                if (_input.GripHeld)
                {
                    if (_staff == null) CreateStaff();
                    UpdateStaffPose();
                }
                else if (_staff != null)
                {
                    Destroy(_staff);
                    _staff = null;
                }

                // Spells are cast only on the rising edge of their button press.
                // If the staff is not summoned, the hand itself is used as the visual origin.
                if (_input.APressed && Time.time >= _nextFire) CastFireball();
                if (_input.TriggerPressed && Time.time >= _nextBolt) CastLightning();
            }
            else if (_staff != null)
            {
                Destroy(_staff);
                _staff = null;
            }

            UpdateEffects();
        }

        private void CreateMaterials()
        {
            _wood = MakeMaterial(_settings.StaffWood);
            _metal = MakeMaterial(_settings.StaffMetal);
            _crystal = MakeMaterial(_settings.StaffCrystal);
            _fire = MakeMaterial(_settings.FireballFlame);
            _bolt = MakeMaterial(_settings.LightningGlow);
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            Material material = new Material(shader);
            if (material.HasProperty("_Color")) material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        private void CreateStaff()
        {
            _staff = new GameObject("GTAGWizard_Staff_Local");
            _staff.transform.SetParent(_hand, false);
            _staff.transform.localPosition = Vector3.zero;
            _staff.transform.localRotation = Quaternion.identity;

            // Staff shaft and decorative pieces. Primitive colliders are immediately removed.
            AddPart(PrimitiveType.Cylinder, _staff.transform, new Vector3(0f, 0.34f, 0f), Quaternion.identity,
                new Vector3(0.045f, 0.38f, 0.045f), _wood);
            AddPart(PrimitiveType.Cylinder, _staff.transform, new Vector3(0f, 0.68f, 0f), Quaternion.identity,
                new Vector3(0.075f, 0.035f, 0.075f), _metal);
            AddPart(PrimitiveType.Sphere, _staff.transform, new Vector3(0f, 0.79f, 0f), Quaternion.identity,
                new Vector3(0.15f, 0.21f, 0.15f), _crystal);
            AddPart(PrimitiveType.Sphere, _staff.transform, new Vector3(0f, 0.79f, 0f), Quaternion.identity,
                new Vector3(0.19f, 0.26f, 0.19f), _crystal, 0.35f);

            ParticleSystem particles = _staff.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 0.65f;
            main.startSpeed = 0.12f;
            main.startSize = 0.035f;
            main.startColor = _settings.StaffCrystal;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 18f * _settings.ParticleIntensity;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            particles.transform.localPosition = new Vector3(0f, 0.79f, 0f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = _crystal;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particles.Play();
        }




        private void UpdateStaffPose()
        {
            if (_staff == null || _hand == null) return;

            _staff.transform.localPosition = _settings.StaffOffset + new Vector3(0.040f, 0.055f, 0f);
            _staff.transform.localRotation = Quaternion.Euler(_settings.StaffRotation + new Vector3(0f, -90f, -90f));
            _staff.transform.localScale = Vector3.one * _settings.StaffScale;
        }




        private static GameObject AddPart(PrimitiveType type, Transform parent, Vector3 position,
            Quaternion rotation, Vector3 scale, Material material, float alpha = 1f)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = "WizardStaffPart";
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return part;
        }

        private Vector3 SpellOrigin()
        {
            Transform source = _staff != null ? _staff.transform : _hand;
            return source.position + source.up * 0.78f + source.forward * 0.12f;
        }

        private void CastFireball()
        {
            _nextFire = Time.time + _settings.SpellCooldown;
            GameObject root = new GameObject("GTAGWizard_Fireball_Local");
            Vector3 start = SpellOrigin();
            Vector3 end = start + (_staff != null ? _staff.transform.up : _hand.forward) * _settings.FireballDistance;
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider collider = core.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.50f;
            root.transform.position = start;
            CreateEffectParticles(root.transform, _settings.FireballFlame, _fire, 42f, 0.55f, 0.045f);
            Renderer renderer = core.GetComponent<Renderer>();
            if (renderer != null && _fire != null) renderer.sharedMaterial = _fire;
            _effects.Add(new TimedEffect { Root = root, Started = Time.time, Duration = _settings.FireballDuration,
                Start = start, End = end, IsFireball = true });
            TrimEffects(true);
        }

        private void CastLightning()
        {
            _nextBolt = Time.time + _settings.SpellCooldown;
            GameObject root = new GameObject("GTAGWizard_Lightning_Local");
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.positionCount = 9;
            line.useWorldSpace = true;
            line.startWidth = 0.055f;
            line.endWidth = 0.012f;
            line.material = _bolt;
            line.startColor = _settings.LightningCore;
            line.endColor = _settings.LightningGlow;
            line.numCapVertices = 2;
            Vector3 start = SpellOrigin();
            Vector3 direction = _staff != null ? _staff.transform.up : _hand.forward;
            Vector3 end = start + direction * _settings.LightningLength;
            root.transform.position = start;
            CreateEffectParticles(root.transform, _settings.LightningSpark, _bolt, 32f, 0.22f, 0.025f);
            _effects.Add(new TimedEffect { Root = root, Started = Time.time, Duration = _settings.LightningDuration,
                Start = start, End = end, IsFireball = false, Line = line });
            TrimEffects(false);
        }

        private void UpdateEffects()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                TimedEffect effect = _effects[i];
                if (effect.Root == null) { _effects.RemoveAt(i); continue; }
                float t = Mathf.Clamp01((Time.time - effect.Started) / Mathf.Max(0.01f, effect.Duration));
                if (effect.IsFireball)
                {
                    effect.Root.transform.position = Vector3.Lerp(effect.Start, effect.End, t);
                    float size = Mathf.Lerp(0.65f, 0.05f, t);
                    effect.Root.transform.localScale = Vector3.one * size;
                }
                else if (effect.Line != null)
                {
                    Vector3 direction = effect.End - effect.Start;
                    Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
                    if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                    side.Normalize();
                    for (int p = 0; p < effect.Line.positionCount; p++)
                    {
                        float along = (float)p / (effect.Line.positionCount - 1);
                        float jitter = (p == 0 || p == effect.Line.positionCount - 1) ? 0f : Mathf.Sin((p * 19.7f) + Time.time * 70f) * 0.12f;
                        effect.Line.SetPosition(p, Vector3.Lerp(effect.Start, effect.End, along) + side * jitter);
                    }
                    effect.Line.startColor = Color.Lerp(_settings.LightningCore, Color.white, Mathf.PingPong(Time.time * 8f, 0.35f));
                    effect.Line.startWidth = Mathf.Lerp(0.055f, 0.002f, t);
                    effect.Line.endWidth = Mathf.Lerp(0.012f, 0.001f, t);
                }
                if (t >= 1f)
                {
                    Destroy(effect.Root);
                    _effects.RemoveAt(i);
                }
            }
        }


        private void CreateEffectParticles(Transform parent, Color color, Material material, float rate, float lifetime, float size)
        {
            GameObject particleObject = new GameObject("WizardSpellParticles");
            particleObject.transform.SetParent(parent, false);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = lifetime;
            main.startSpeed = 0.18f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = 96;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate * _settings.ParticleIntensity;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.07f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            particles.Play();
        }

        private void TrimEffects(bool fireball)
        {
            int count = 0;
            for (int i = 0; i < _effects.Count; i++) if (_effects[i].IsFireball == fireball) count++;
            for (int i = 0; count > _settings.MaxActiveEffects && i < _effects.Count;)
            {
                if (_effects[i].IsFireball == fireball)
                {
                    if (_effects[i].Root != null) Destroy(_effects[i].Root);
                    _effects.RemoveAt(i);
                    count--;
                }
                else i++;
            }
        }

        private void OnDestroy()
        {
            if (_staff != null) Destroy(_staff);
            for (int i = 0; i < _effects.Count; i++) if (_effects[i].Root != null) Destroy(_effects[i].Root);
            _effects.Clear();
            if (_wood != null) Destroy(_wood);
            if (_metal != null) Destroy(_metal);
            if (_crystal != null) Destroy(_crystal);
            if (_fire != null) Destroy(_fire);
            if (_bolt != null) Destroy(_bolt);
        }
    }
}
