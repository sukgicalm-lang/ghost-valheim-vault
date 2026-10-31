using System;
using System.Reflection;
using HarmonyLib;

namespace SmelterControl
{
    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class SmelterAwakePatch
    {
        private static void Postfix(Smelter __instance)
        {
            try
            {
                SmelterApplier.Apply(__instance);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce(ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    internal static class ZNetSceneAwakePatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ZNetScene __instance)
        {
            try
            {
                SmelterApplier.ApplyPrefabs(__instance);
                Plugin.ReapplyAfterSceneAwake(__instance);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce(ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "RPC_AddOre")]
    internal static class AddOreCapPatch
    {
        private static readonly MethodInfo QueueSize = AccessTools.Method(typeof(Smelter), "GetQueueSize");
        private static readonly FieldInfo NetView = AccessTools.Field(typeof(Smelter), "m_nview");

        private static bool Prefix(Smelter __instance)
        {
            try
            {
                if (QueueSize == null)
                {
                    Plugin.LogOnce("Smelter.GetQueueSize was missing.");
                }

                if (NetView == null)
                {
                    Plugin.LogOnce("Smelter.m_nview was missing.");
                }

                if (__instance == null || __instance.gameObject == null
                    || !SmelterRules.IsControlled(SmelterRules.PrefabName(__instance.gameObject.name)))
                {
                    return true;
                }

                if (!IsOwner(__instance))
                {
                    return true;
                }

                int queue = 0;
                if (QueueSize != null)
                {
                    object value = QueueSize.Invoke(__instance, null);
                    if (value is int count)
                    {
                        queue = count;
                    }
                }

                return !SmelterRules.OreIsFull(queue, __instance.m_maxOre);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce(ex.Message);
                return true;
            }
        }

        internal static bool IsOwner(Smelter smelter)
        {
            if (smelter == null)
            {
                return false;
            }

            if (NetView == null)
            {
                Plugin.LogOnce("Smelter.m_nview was missing.");
                return false;
            }

            ZNetView view = NetView.GetValue(smelter) as ZNetView;
            return view != null && view.IsValid() && view.IsOwner();
        }
    }

    [HarmonyPatch(typeof(Smelter), "RPC_AddFuel")]
    internal static class AddFuelCapPatch
    {
        private static readonly MethodInfo FuelAmount = AccessTools.Method(typeof(Smelter), "GetFuel");

        private static bool Prefix(Smelter __instance)
        {
            try
            {
                if (FuelAmount == null)
                {
                    Plugin.LogOnce("Smelter.GetFuel was missing.");
                }

                if (__instance == null || __instance.gameObject == null
                    || !SmelterRules.IsControlled(SmelterRules.PrefabName(__instance.gameObject.name)))
                {
                    return true;
                }

                if (!AddOreCapPatch.IsOwner(__instance) || __instance.m_fuelItem == null)
                {
                    return true;
                }

                float fuel = 0f;
                if (FuelAmount != null)
                {
                    object value = FuelAmount.Invoke(__instance, null);
                    if (value is float amount)
                    {
                        fuel = amount;
                    }
                }

                return !SmelterRules.FuelIsFull(fuel, __instance.m_maxFuel);
            }
            catch (Exception ex)
            {
                Plugin.LogOnce(ex.Message);
                return true;
            }
        }
    }
}
