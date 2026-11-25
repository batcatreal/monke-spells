using BepInEx;
using GTAGWizard.Config;
using GTAGWizard.Core;
using UnityEngine;

namespace GTAGWizard
{
    /// <summary>
    /// BepInEx entry point.
    ///
    /// Lifecycle:
    ///   Awake     -> bind config (no game objects yet)
    ///   OnEnable  -> spawn the controller GameObject (all visuals + input live there)
    ///   OnDisable -> destroy the controller GameObject (staff, spells, materials, textures, meshes)
    ///
    /// Because the mod uses no Harmony patches, no hooks into game methods and no networking,
    /// disabling the plugin (or removing the DLL) leaves nothing behind.
    /// </summary>
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInProcess("Gorilla Tag.exe")] // never load into any other Unity game
    public sealed class Plugin : BaseUnityPlugin
    {
        private WizardSettings _settings;
        private GameObject _controllerObject;

        private void Awake()
        {
            _settings = new WizardSettings(Config);
            Logger.LogInfo(PluginInfo.Name + " " + PluginInfo.Version + " loaded. Cosmetic/visual only; local to this client.");
        }

        private void OnEnable()
        {
            Spawn();
        }

        private void OnDisable()
        {
            Despawn();
        }

        private void OnDestroy()
        {
            Despawn();
        }

        private void Spawn()
        {
            if (_settings == null || _controllerObject != null) return;

            _controllerObject = new GameObject("GTAGWizardMod.Controller");
            DontDestroyOnLoad(_controllerObject); // survive scene loads; we destroy it ourselves in Despawn

            WizardController controller = _controllerObject.AddComponent<WizardController>();
            controller.Initialize(Logger, _settings);
        }

        private void Despawn()
        {
            if (_controllerObject == null) return;
            Destroy(_controllerObject); // WizardController.OnDestroy releases everything it created
            _controllerObject = null;
        }
    }
}
