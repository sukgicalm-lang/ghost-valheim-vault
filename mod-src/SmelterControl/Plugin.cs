using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SmelterControl
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInIncompatibility("com.frizzqol.smeltercontrol")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ghost.valheim.smeltercontrol";
        public const string PluginName = "Ghost Smelter Control";
        public const string PluginVersion = "1.0.1";

        internal static Plugin Instance { get; private set; }

        internal struct StationSettings
        {
            public float Seconds;
            public int Input;
            public int Fuel;
        }

        internal static ConfigEntry<float> SmelterSeconds;
        internal static ConfigEntry<int> SmelterOre;
        internal static ConfigEntry<int> SmelterFuel;
        internal static ConfigEntry<float> BlastSeconds;
        internal static ConfigEntry<int> BlastOre;
        internal static ConfigEntry<int> BlastFuel;
        internal static ConfigEntry<float> KilnSeconds;
        internal static ConfigEntry<int> KilnWood;
        internal static ConfigEntry<float> WindmillSeconds;
        internal static ConfigEntry<int> WindmillBarley;

        private static readonly HashSet<string> Logged = new HashSet<string>();
        private const long ConfigReloadQuietTicks = 250L * TimeSpan.TicksPerMillisecond;
        private Harmony _harmony;
        private FileSystemWatcher _configWatcher;
        private long _configQuietUntilTicks;
        private int _reloadQueued;
        private int _applyQueued;

        private void Awake()
        {
            Instance = this;
            SmelterSeconds = Config.Bind("Smelter", "Seconds", 1f, "Seconds per bar (default 1). 0 or below restores the captured prefab value.");
            SmelterOre = Config.Bind("Smelter", "Ore", 100, "Maximum ore held (default 100). 0 or below restores the captured prefab value.");
            SmelterFuel = Config.Bind("Smelter", "Fuel", 100, "Maximum coal held (default 100). 0 or below restores the captured prefab value.");
            BlastSeconds = Config.Bind("BlastFurnace", "Seconds", 1f, "Seconds per bar (default 1). 0 or below restores the captured prefab value.");
            BlastOre = Config.Bind("BlastFurnace", "Ore", 100, "Maximum ore held (default 100). 0 or below restores the captured prefab value.");
            BlastFuel = Config.Bind("BlastFurnace", "Fuel", 100, "Maximum coal held (default 100). 0 or below restores the captured prefab value.");
            KilnSeconds = Config.Bind("CharcoalKiln", "Seconds", 1f, "Seconds per coal (default 1). 0 or below restores the captured prefab value.");
            KilnWood = Config.Bind("CharcoalKiln", "Wood", 100, "Maximum wood held (default 100). 0 or below restores the captured prefab value.");
            WindmillSeconds = Config.Bind("Windmill", "Seconds", 1f, "Base seconds per barley flour at full wind power (default 1). Native wind strength and cover still affect actual processing time. 0 or below restores the captured prefab value.");
            WindmillBarley = Config.Bind("Windmill", "Barley", 100, "Maximum barley held (default 100). This is storage capacity, not output yield or items per click. 0 or below restores the captured prefab value.");
            Config.SettingChanged += (_, __) => QueueApply();
            WatchConfigFile();

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
                Logger.LogInfo(PluginName + " " + PluginVersion + " loaded");
            }
            catch (Exception ex)
            {
                Logger.LogError("Harmony patch failed: " + ex.ToString());
                _harmony.UnpatchSelf();
            }
        }

        private void QueueApply()
        {
            if (Interlocked.CompareExchange(ref _applyQueued, 1, 0) != 0)
            {
                return;
            }

            // Consumed by Update on Unity's main thread, including file watcher events.
        }

        private void ApplyQueued()
        {
            Interlocked.Exchange(ref _applyQueued, 0);
            SmelterApplier.ApplyAll();
        }

        private void Update()
        {
            if (Volatile.Read(ref _reloadQueued) != 0
                && DateTime.UtcNow.Ticks >= Interlocked.Read(ref _configQuietUntilTicks)
                && Interlocked.Exchange(ref _reloadQueued, 0) != 0)
            {
                try { ReloadConfigFile(); }
                catch (Exception ex) { LogOnce("Config reload failed: " + ex.Message); }
            }
            if (Volatile.Read(ref _applyQueued) != 0)
            {
                try { ApplyQueued(); }
                catch (Exception ex) { LogOnce("Config apply failed: " + ex.Message); }
            }
        }

        internal static void ReapplyAfterSceneAwake(ZNetScene scene)
        {
            if (Instance != null) Instance.StartCoroutine(Instance.ApplyAfterSceneAwake(scene));
        }

        private IEnumerator ApplyAfterSceneAwake(ZNetScene scene)
        {
            // ZNetScene in 1.0.20 has Awake but NO Start method. Do not patch Start.
            // Two bounded retries allow other mods to finish registering prefabs.
            yield return null;
            ApplySceneSafely(scene, false);
            yield return new WaitForSeconds(1f);
            ApplySceneSafely(scene, true);
        }

        private static void ApplySceneSafely(ZNetScene scene, bool reportMissing)
        {
            if (scene == null || ZNetScene.instance != scene) return;
            try
            {
                SmelterApplier.ApplyPrefabs(scene, reportMissing);
                SmelterApplier.ApplyAll();
            }
            catch (Exception ex) { LogOnce("Scene config apply failed: " + ex.Message); }
        }

        private void OnDestroy()
        {
            if (_configWatcher != null)
            {
                _configWatcher.EnableRaisingEvents = false;
                _configWatcher.Changed -= OnConfigFileChanged;
                _configWatcher.Created -= OnConfigFileChanged;
                _configWatcher.Renamed -= OnConfigFileChanged;
                _configWatcher.Dispose();
            }
            if (_harmony != null) _harmony.UnpatchSelf();
            Instance = null;
        }

        private void WatchConfigFile()
        {
            string path = Config.ConfigFilePath;
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return;
            }

            _configWatcher = new FileSystemWatcher(directory, fileName);
            _configWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
            _configWatcher.Renamed += OnConfigFileChanged;
            _configWatcher.EnableRaisingEvents = true;
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs args)
        {
            Interlocked.Exchange(ref _configQuietUntilTicks, DateTime.UtcNow.Ticks + ConfigReloadQuietTicks);
            Interlocked.Exchange(ref _reloadQueued, 1);
        }

        private void ReloadConfigFile()
        {
            Config.Reload();
        }

        internal static StationSettings ConfigFor(string prefabName)
        {
            if (prefabName == SmelterRules.WindmillPrefab)
            {
                return new StationSettings
                {
                    Seconds = ReadSeconds(WindmillSeconds, "Windmill Seconds"),
                    Input = ReadCount(WindmillBarley, "Windmill Barley"),
                    Fuel = 0
                };
            }

            if (prefabName == SmelterRules.BlastFurnacePrefab)
            {
                return new StationSettings
                {
                    Seconds = ReadSeconds(BlastSeconds, "BlastFurnace Seconds"),
                    Input = ReadCount(BlastOre, "BlastFurnace Ore"),
                    Fuel = ReadCount(BlastFuel, "BlastFurnace Fuel")
                };
            }

            if (prefabName == SmelterRules.CharcoalKilnPrefab)
            {
                return new StationSettings
                {
                    Seconds = ReadSeconds(KilnSeconds, "CharcoalKiln Seconds"),
                    Input = ReadCount(KilnWood, "CharcoalKiln Wood"),
                    Fuel = 0
                };
            }

            if (prefabName == SmelterRules.SmelterPrefab)
            {
                return new StationSettings
                {
                    Seconds = ReadSeconds(SmelterSeconds, "Smelter Seconds"),
                    Input = ReadCount(SmelterOre, "Smelter Ore"),
                    Fuel = ReadCount(SmelterFuel, "Smelter Fuel")
                };
            }

            return new StationSettings
            {
                Seconds = 0f,
                Input = 0,
                Fuel = 0
            };
        }

        internal static void LogOnce(string message)
        {
            if (string.IsNullOrEmpty(message) || Logged.Contains(message))
            {
                return;
            }

            Logged.Add(message);
            LogWarning(message);
        }

        internal static void LogError(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogError(message);
            }
        }

        internal static void LogInfoOnce(string message)
        {
            if (Instance != null && Logged.Add(message)) Instance.Logger.LogInfo(message);
        }

        private static float ReadSeconds(ConfigEntry<float> entry, string label)
        {
            float value = entry != null ? entry.Value : 0f;
            if (value < 0f)
            {
                LogOnce(label + " below 0 keeps the normal value.");
            }

            return value;
        }

        private static int ReadCount(ConfigEntry<int> entry, string label)
        {
            int value = entry != null ? entry.Value : 0;
            if (value < 0)
            {
                LogOnce(label + " below 0 keeps the normal value.");
            }

            return value;
        }

        private static void LogWarning(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogWarning(message);
            }
        }
    }
}
