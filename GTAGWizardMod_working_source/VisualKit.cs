using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Rendering;

namespace GTAGWizard.Visuals
{
    /// <summary>
    /// Owns every runtime-created Unity resource (shaders are looked up, materials/textures/meshes are created)
    /// and destroys them all in Dispose(), so disabling the mod leaves nothing behind.
    ///
    /// Shaders: we can't assume which shaders the game build contains (Shader.Find only finds shaders that were
    /// included in the build), so each need has an ordered fallback list. The chosen shader is written to the
    /// BepInEx log ("VisualKit: ...") so we can tune the lists from a real log instead of guessing.
    /// If nothing usable is found, the mod degrades (no glow / no solid staff) and logs a warning; it never throws.
    /// </summary>
    internal sealed class VisualKit : IDisposable
    {
        // Glow / particle / lightning shader candidates (any "additive-looking" unlit shader).
        private static readonly string[] AdditiveShaders =
        {
            "Legacy Shaders/Particles/Additive",
            "Mobile/Particles/Additive",
            "Particles/Additive",
            "Sprites/Default",
        };

        // Solid (staff body) shader candidates. Unlit ones first: Gorilla Tag scenes use baked lighting,
        // so a lit shader could end up nearly black.
        private static readonly string[] SolidShadersBuiltin =
        {
            "Hidden/Internal-Colored",
            "Unlit/Color",
            "Legacy Shaders/Diffuse",
            "Standard",
            "GorillaTag/UberShader",
            "Sprites/Default",
        };

        private static readonly string[] SolidShadersSrp =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Lit",
            "Hidden/Internal-Colored",
            "Unlit/Color",
            "GorillaTag/UberShader",
            "Sprites/Default",
        };

        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private readonly Shader _solidShader;

        /// <summary>Additive material with a soft round sprite texture (particles).</summary>
        public Material AdditiveSoft { get; private set; }

        /// <summary>Additive material with a soft stripe texture (LineRenderer bolts).</summary>
        public Material AdditiveLine { get; private set; }

        public bool CanRenderParticles { get { return AdditiveSoft != null && AdditiveLine != null; } }
        public bool CanRenderSolids { get { return _solidShader != null; } }

        public VisualKit(ManualLogSource log)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            log.LogInfo("VisualKit: render pipeline = " + (srp ? "scriptable (URP/HDRP)" : "built-in"));

            Shader additive = FindFirst(log, "glow/particles", AdditiveShaders);
            if (additive != null)
            {
                AdditiveSoft = CreateAdditive(additive, CreateSoftCircle(64), "GTAGWizard_AdditiveSoft");
                AdditiveLine = CreateAdditive(additive, CreateSoftStripe(4, 64), "GTAGWizard_AdditiveLine");
            }
            else
            {
                log.LogWarning("VisualKit: no usable glow shader found; particles and lightning will not be drawn.");
            }

            _solidShader = FindFirst(log, "solid meshes", srp ? SolidShadersSrp : SolidShadersBuiltin);
            if (_solidShader == null)
                log.LogWarning("VisualKit: no usable solid shader found; the staff body will not be drawn.");
        }

        /// <summary>Creates a tracked solid-colour material, or null if no solid shader exists.</summary>
        public Material CreateSolid(string name, Color color, float emission)
        {
            if (_solidShader == null) return null;

            Material material = new Material(_solidShader);
            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            return Own(material);
        }

        /// <summary>Registers any Unity object (mesh, texture, material) for destruction in Dispose().</summary>
        public T Own<T>(T resource) where T : UnityEngine.Object
        {
            if (resource != null) _owned.Add(resource);
            return resource;
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            }
            _owned.Clear();
            AdditiveSoft = null;
            AdditiveLine = null;
        }

        // ---------------------------------------------------------------- internals

        private static Shader FindFirst(ManualLogSource log, string purpose, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null && shader.isSupported)
                {
                    log.LogInfo("VisualKit: shader for " + purpose + " = " + names[i]);
                    return shader;
                }
            }
            return null;
        }

        private Material CreateAdditive(Shader shader, Texture2D texture, string name)
        {
            Material material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_MainTex")) material.mainTexture = texture;
            // Colour is supplied per-particle / per-vertex, so the material's own tint stays at the shader default.
            return Own(material);
        }

        /// <summary>White disc with a smooth radial alpha falloff, used for glows, flames and sparks.</summary>
        private Texture2D CreateSoftCircle(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "GTAGWizard_SoftCircle";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true); // upload, then drop the CPU copy
            return Own(texture);
        }

        /// <summary>
        /// Texture for line renderers in Stretch mode: U runs along the line (constant), V runs across it
        /// (bright centre, soft edges), so the bolt looks soft-edged along its whole length.
        /// </summary>
        private Texture2D CreateSoftStripe(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = "GTAGWizard_SoftStripe";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[width * height];
            float centre = (height - 1) * 0.5f;
            for (int y = 0; y < height; y++)
            {
                float distance = Mathf.Abs((y - centre) / centre);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Own(texture);
        }
    }
}
