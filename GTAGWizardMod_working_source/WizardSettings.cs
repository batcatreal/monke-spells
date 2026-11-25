using BepInEx.Configuration;
using GTAGWizard.Visuals;
using UnityEngine;

namespace GTAGWizard.Config
{
    /// <summary>
    /// Every user-tweakable value (Feature 4). Backed by BepInEx config:
    ///   BepInEx\config\com.gtagwizard.wizardmod.cfg
    /// Colours are hex strings such as "#FF6A1A" so they are easy to edit by hand.
    ///
    /// Three sections:
    ///   Appearance - colours, particle intensity, render layer. Changing these rebuilds the visuals.
    ///   Spells     - cooldown / distance / duration. Read every cast, no rebuild needed.
    ///   Placement  - where/how the staff sits in the hand. Read every frame, so it can be tuned live.
    /// </summary>
    internal sealed class WizardSettings
    {
        private const string Appearance = "Appearance";
        private const string Spells = "Spells";
        private const string Placement = "Placement";

        // Default colours. Also used as the fallback when a hex string in the config is invalid.
        private const string WoodHex = "#5B3A21";
        private const string MetalHex = "#C9A24B";
        private const string CrystalHex = "#7F6BFF";
        private const string FireCoreHex = "#FFE9A0";
        private const string FireFlameHex = "#FF6A1A";
        private const string FireEmberHex = "#FF2E0A";
        private const string BoltCoreHex = "#EAF4FF";
        private const string BoltGlowHex = "#8A5CFF";
        private const string BoltSparkHex = "#6FD3FF";

        private readonly ConfigEntry<string> _wood, _metal, _crystal;
        private readonly ConfigEntry<string> _fireCore, _fireFlame, _fireEmber;
        private readonly ConfigEntry<string> _boltCore, _boltGlow, _boltSpark;
        private readonly ConfigEntry<float> _intensity;
        private readonly ConfigEntry<int> _layer;

        private readonly ConfigEntry<float> _cooldown, _fireDistance, _fireDuration, _boltRange, _boltDuration;
        private readonly ConfigEntry<int> _maxActive;

        private readonly ConfigEntry<float> _scale, _posX, _posY, _posZ, _rotX, _rotY, _rotZ;

        private bool _visualsDirty;

        public WizardSettings(ConfigFile file)
        {
            // ---- Appearance ----
            _wood = file.Bind(Appearance, "StaffWoodColor", WoodHex, "Hex colour (#RRGGBB) of the staff shaft.");
            _metal = file.Bind(Appearance, "StaffMetalColor", MetalHex, "Hex colour of the staff's metal bands and claws.");
            _crystal = file.Bind(Appearance, "StaffCrystalColor", CrystalHex, "Hex colour of the glowing crystal and its particles.");
            _fireCore = file.Bind(Appearance, "FireballCoreColor", FireCoreHex, "Hot centre colour of the fireball.");
            _fireFlame = file.Bind(Appearance, "FireballFlameColor", FireFlameHex, "Main flame colour of the fireball.");
            _fireEmber = file.Bind(Appearance, "FireballEmberColor", FireEmberHex, "Outer flame / ember colour of the fireball.");
            _boltCore = file.Bind(Appearance, "LightningCoreColor", BoltCoreHex, "Bright inner colour of the lightning bolt.");
            _boltGlow = file.Bind(Appearance, "LightningGlowColor", BoltGlowHex, "Outer glow colour of the lightning bolt.");
            _boltSpark = file.Bind(Appearance, "LightningSparkColor", BoltSparkHex, "Colour of the electric sparks around the bolt.");

            _intensity = file.Bind(Appearance, "ParticleIntensity", 1f,
                new ConfigDescription("Multiplier for particle counts and flash brightness. Lower this if you want less visual noise or more FPS.",
                    new AcceptableValueRange<float>(0.1f, 2f)));

            _layer = file.Bind(Appearance, "RenderLayer", 0,
                new ConfigDescription("Unity layer used for the staff and spells. Only change this if the effects are invisible in-game (camera culling).",
                    new AcceptableValueRange<int>(0, 31)));

            // ---- Spells ----
            _cooldown = file.Bind(Spells, "CastCooldownSeconds", 0.35f,
                new ConfigDescription("Minimum seconds between casts of the same spell (spells also fire only once per button press).",
                    new AcceptableValueRange<float>(0.1f, 3f)));
            _maxActive = file.Bind(Spells, "MaxSimultaneousEffectsPerSpell", 6,
                new ConfigDescription("Hard cap on how many copies of one spell can be on screen at once.",
                    new AcceptableValueRange<int>(1, 12)));
            _fireDistance = file.Bind(Spells, "FireballTravelDistance", 4f,
                new ConfigDescription("How far (metres) the fireball visual drifts forward. Purely a drawn animation.",
                    new AcceptableValueRange<float>(0.5f, 15f)));
            _fireDuration = file.Bind(Spells, "FireballDurationSeconds", 1.2f,
                new ConfigDescription("Seconds until the fireball visual burns out.",
                    new AcceptableValueRange<float>(0.4f, 4f)));
            _boltRange = file.Bind(Spells, "LightningLength", 6f,
                new ConfigDescription("Length (metres) of the lightning bolt visual.",
                    new AcceptableValueRange<float>(1f, 20f)));
            _boltDuration = file.Bind(Spells, "LightningDurationSeconds", 0.3f,
                new ConfigDescription("Seconds the lightning bolt flickers before fading.",
                    new AcceptableValueRange<float>(0.1f, 1f)));

            // ---- Placement (live-tunable, e.g. with the BepInEx ConfigurationManager F1 window) ----
            _scale = file.Bind(Placement, "StaffScale", 1f,
                new ConfigDescription("Overall staff size multiplier.", new AcceptableValueRange<float>(0.3f, 3f)));
            _posX = file.Bind(Placement, "OffsetX", 0f,
                new ConfigDescription("Staff position offset in the hand's local space (metres).", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _posY = file.Bind(Placement, "OffsetY", 0f,
                new ConfigDescription("Staff position offset in the hand's local space (metres).", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _posZ = file.Bind(Placement, "OffsetZ", 0f,
                new ConfigDescription("Staff position offset in the hand's local space (metres).", new AcceptableValueRange<float>(-0.5f, 0.5f)));
            _rotX = file.Bind(Placement, "RotationX", 90f,
                new ConfigDescription("Staff rotation offset (degrees) applied on top of the hand rotation. Default tilts the staff forward.",
                    new AcceptableValueRange<float>(-180f, 180f)));
            _rotY = file.Bind(Placement, "RotationY", 0f,
                new ConfigDescription("Staff rotation offset (degrees).", new AcceptableValueRange<float>(-180f, 180f)));
            _rotZ = file.Bind(Placement, "RotationZ", 0f,
                new ConfigDescription("Staff rotation offset (degrees).", new AcceptableValueRange<float>(-180f, 180f)));

            // Only Appearance changes need a rebuild; the rest is read live.
            file.SettingChanged += OnSettingChanged;
        }

        private void OnSettingChanged(object sender, SettingChangedEventArgs e)
        {
            if (e != null && e.ChangedSetting != null && e.ChangedSetting.Definition.Section != Appearance) return;
            _visualsDirty = true;
        }

        /// <summary>True once after an Appearance setting changed; the controller then rebuilds visuals.</summary>
        public bool ConsumeVisualsDirty()
        {
            bool dirty = _visualsDirty;
            _visualsDirty = false;
            return dirty;
        }

        // ---- Appearance ----
        public Color StaffWood { get { return ColorUtil.Parse(_wood.Value, WoodHex); } }
        public Color StaffMetal { get { return ColorUtil.Parse(_metal.Value, MetalHex); } }
        public Color StaffCrystal { get { return ColorUtil.Parse(_crystal.Value, CrystalHex); } }
        public Color FireballCore { get { return ColorUtil.Parse(_fireCore.Value, FireCoreHex); } }
        public Color FireballFlame { get { return ColorUtil.Parse(_fireFlame.Value, FireFlameHex); } }
        public Color FireballEmber { get { return ColorUtil.Parse(_fireEmber.Value, FireEmberHex); } }
        public Color LightningCore { get { return ColorUtil.Parse(_boltCore.Value, BoltCoreHex); } }
        public Color LightningGlow { get { return ColorUtil.Parse(_boltGlow.Value, BoltGlowHex); } }
        public Color LightningSpark { get { return ColorUtil.Parse(_boltSpark.Value, BoltSparkHex); } }
        public float ParticleIntensity { get { return Mathf.Clamp(_intensity.Value, 0.1f, 2f); } }
        public int RenderLayer { get { return Mathf.Clamp(_layer.Value, 0, 31); } }

        // ---- Spells ----
        public float SpellCooldown { get { return _cooldown.Value; } }
        public int MaxActiveEffects { get { return _maxActive.Value; } }
        public float FireballDistance { get { return _fireDistance.Value; } }
        public float FireballDuration { get { return _fireDuration.Value; } }
        public float LightningLength { get { return _boltRange.Value; } }
        public float LightningDuration { get { return _boltDuration.Value; } }

        // ---- Placement ----
        public float StaffScale { get { return _scale.Value; } }
        public Vector3 StaffOffset { get { return new Vector3(_posX.Value, _posY.Value, _posZ.Value); } }
        public Vector3 StaffRotation { get { return new Vector3(_rotX.Value, _rotY.Value, _rotZ.Value); } }
    }
}
