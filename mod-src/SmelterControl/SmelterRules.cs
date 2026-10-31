namespace SmelterControl
{
    public static class SmelterRules
    {
        // These are network prefab IDs, NOT the $piece_* localization keys.
        public const string SmelterPrefab = "smelter";
        public const string BlastFurnacePrefab = "blastfurnace";
        public const string CharcoalKilnPrefab = "charcoal_kiln";
        public const string WindmillPrefab = "windmill";

        public static float ResolveSeconds(float configured, float vanilla)
        {
            if (configured <= 0f || float.IsNaN(configured) || float.IsInfinity(configured))
            {
                return vanilla;
            }

            return configured;
        }

        public static int ResolveCount(int configured, int vanilla)
        {
            if (configured <= 0)
            {
                return vanilla;
            }

            return configured;
        }

        public static bool IsControlled(string prefabName)
        {
            return prefabName == SmelterPrefab
                || prefabName == BlastFurnacePrefab
                || prefabName == CharcoalKilnPrefab
                || prefabName == WindmillPrefab;
        }

        public static bool WritesFuel(string prefabName, bool hasFuelItem)
        {
            if (!hasFuelItem)
            {
                return false;
            }

            return prefabName == SmelterPrefab || prefabName == BlastFurnacePrefab;
        }

        public static bool OreIsFull(int queueSize, int maxOre)
        {
            return queueSize >= maxOre;
        }

        public static bool FuelIsFull(float fuel, int maxFuel)
        {
            return fuel > maxFuel - 1;
        }

        public static string PrefabName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return "";
            }

            const string suffix = "(Clone)";
            return objectName.EndsWith(suffix, System.StringComparison.Ordinal)
                ? objectName.Substring(0, objectName.Length - suffix.Length).TrimEnd()
                : objectName;
        }
    }
}
