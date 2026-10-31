using System.Collections.Generic;
using UnityEngine;

namespace SmelterControl
{
    internal static class SmelterApplier
    {
        private struct VanillaStats
        {
            public float Seconds;
            public int Ore;
            public int Fuel;
        }

        private static readonly Dictionary<string, VanillaStats> Vanilla = new Dictionary<string, VanillaStats>();

        internal static void Apply(Smelter smelter)
        {
            if (smelter == null)
            {
                return;
            }

            string objectName = smelter.gameObject != null ? smelter.gameObject.name : null;
            ApplyNamed(smelter, SmelterRules.PrefabName(objectName));
        }

        internal static void ApplyPrefabs(ZNetScene scene, bool reportMissing = false)
        {
            if (scene == null)
            {
                return;
            }

            // Awake populates the hash registry from both prefab lists. Prefer the
            // game's lookup; retain the lists as a fallback during scene initialization.
            foreach (string id in new[] { SmelterRules.SmelterPrefab,
                SmelterRules.BlastFurnacePrefab, SmelterRules.CharcoalKilnPrefab, SmelterRules.WindmillPrefab })
            {
                GameObject prefab = scene.GetPrefab(id);
                if (prefab == null) prefab = FindPrefab(scene.m_prefabs, id);
                if (prefab == null) prefab = FindPrefab(scene.m_nonNetViewPrefabs, id);
                Smelter smelter = prefab != null ? prefab.GetComponent<Smelter>() : null;
                if (smelter != null)
                {
                    ApplyNamed(smelter, id);
                    Plugin.LogInfoOnce("Applied " + id + ": seconds=" + smelter.m_secPerProduct
                        + ", input=" + smelter.m_maxOre + ", fuel=" + smelter.m_maxFuel);
                }
                else if (reportMissing)
                {
                    Plugin.LogOnce("Missing smelter prefab or root Smelter component after scene registration: " + id);
                }
            }
        }

        private static GameObject FindPrefab(List<GameObject> prefabs, string id)
        {
            if (prefabs == null) return null;
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab != null && prefab.name == id) return prefab;
            }
            return null;
        }

        internal static void ApplyAll()
        {
            if (ZNetScene.instance != null)
            {
                ApplyPrefabs(ZNetScene.instance);
            }

            Smelter[] loaded = Object.FindObjectsByType<Smelter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < loaded.Length; i++)
            {
                Apply(loaded[i]);
            }
        }

        private static void ApplyNamed(Smelter smelter, string prefabName)
        {
            if (smelter == null || !SmelterRules.IsControlled(prefabName))
            {
                return;
            }

            if (!Vanilla.ContainsKey(prefabName))
            {
                Vanilla[prefabName] = new VanillaStats
                {
                    Seconds = smelter.m_secPerProduct,
                    Ore = smelter.m_maxOre,
                    Fuel = smelter.m_maxFuel
                };
            }

            VanillaStats vanilla = Vanilla[prefabName];
            Plugin.StationSettings settings = Plugin.ConfigFor(prefabName);
            smelter.m_secPerProduct = SmelterRules.ResolveSeconds(settings.Seconds, vanilla.Seconds);
            smelter.m_maxOre = SmelterRules.ResolveCount(settings.Input, vanilla.Ore);
            if (SmelterRules.WritesFuel(prefabName, smelter.m_fuelItem != null))
            {
                smelter.m_maxFuel = SmelterRules.ResolveCount(settings.Fuel, vanilla.Fuel);
            }
        }
    }
}
