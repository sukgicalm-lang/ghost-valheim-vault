// Managed simulation only: Unity's native scene and rendering are not run here.
using System;
using System.Collections.Generic;
using System.Linq;
using SmelterControl;
using UnityEngine;
using Xunit;

[CollectionDefinition("Applier", DisableParallelization = true)]
public class ApplierCollection { }

[Collection("Applier")]
public class ApplierTests
{
    private static Smelter Station(string id, float seconds = 30f, int input = 10,
        int fuel = 20, bool hasFuel = true)
    {
        return new Smelter { gameObject = new GameObject { name = id },
            m_secPerProduct = seconds, m_maxOre = input, m_maxFuel = fuel,
            m_fuelItem = hasFuel ? new ItemDrop() : null };
    }

    [Fact]
    public void Registry_fallbacks_instances_settings_and_exclusions()
    {
        Plugin.Settings.Clear(); Plugin.Messages.Clear();
        var smelter = Station("smelter");
        var blast = Station("blastfurnace");
        var kiln = Station("charcoal_kiln", 15, 25, 0, false);
        var windmill = Station("windmill", 10, 50, 0, false);
        foreach (var s in new[] { smelter, blast, kiln, windmill })
        {
            s.gameObject.component = s;
            Plugin.Settings[s.gameObject.name] = new Plugin.StationSettings
                { Seconds = 1, Input = 100, Fuel = 100 };
        }
        var scene = new ZNetScene(); ZNetScene.instance = scene;
        scene.Registry["smelter"] = smelter.gameObject;
        scene.m_prefabs.Add(blast.gameObject);
        scene.m_nonNetViewPrefabs.Add(kiln.gameObject);
        scene.m_prefabs.Add(windmill.gameObject);
        SmelterApplier.ApplyPrefabs(scene, true);
        Assert.DoesNotContain(Plugin.Messages, m => m.Contains("Missing"));
        foreach (var s in new[] { smelter, blast, kiln, windmill })
        { Assert.Equal(1f, s.m_secPerProduct); Assert.Equal(100, s.m_maxOre); }
        Assert.Equal(100, smelter.m_maxFuel); Assert.Equal(100, blast.m_maxFuel);
        Assert.Equal(0, kiln.m_maxFuel); // no artificial fuel slot
        Assert.Equal(0, windmill.m_maxFuel);

        var loaded = Station("smelter(Clone)", 1, 100, 100);
        var inactive = Station("blastfurnace(Clone)", 1, 100, 100);
        var unrelated = new[] { "piece_windmill", "piece_spinningwheel", "piece_eitrrefinery",
            "piece_bathtub", "piece_FrostKiln", "GhostSmelter", "piece_smelter",
            "smelter(Clone)_custom" }.Select(id => Station(id)).ToArray();
        UnityEngine.Object.Loaded = new[] { loaded, inactive }.Concat(unrelated).ToArray();
        Plugin.Settings["smelter"] = new Plugin.StationSettings { Seconds = 2, Input = 50, Fuel = 60 };
        SmelterApplier.ApplyAll();
        Assert.Equal(2f, loaded.m_secPerProduct); Assert.Equal(50, loaded.m_maxOre);
        Assert.Equal(60, loaded.m_maxFuel);
        Assert.True(UnityEngine.Object.IncludedInactive);
        Assert.Equal(100, inactive.m_maxOre);
        foreach (var other in unrelated)
        { Assert.Equal(30f, other.m_secPerProduct); Assert.Equal(10, other.m_maxOre);
          Assert.Equal(20, other.m_maxFuel); }

        foreach (string id in Plugin.Settings.Keys.ToArray())
            Plugin.Settings[id] = new Plugin.StationSettings(); // restore prefab baseline
        SmelterApplier.ApplyAll();
        Assert.Equal(30f, loaded.m_secPerProduct); Assert.Equal(10, loaded.m_maxOre);
        Assert.Equal(20, loaded.m_maxFuel); Assert.Equal(30f, inactive.m_secPerProduct);
        Assert.Equal(15f, kiln.m_secPerProduct); Assert.Equal(25, kiln.m_maxOre);
        Assert.Equal(0, kiln.m_maxFuel);
        Assert.Equal(10f, windmill.m_secPerProduct); Assert.Equal(50, windmill.m_maxOre);
        Assert.Equal(0, windmill.m_maxFuel);
        SmelterApplier.Apply(null);
        SmelterApplier.ApplyPrefabs(null);
    }

    [Fact]
    public void Existing_inactive_windmill_reloads_settings_without_changing_fuel_or_other_stations()
    {
        Plugin.Settings.Clear(); Plugin.Messages.Clear();
        var prefab = Station("windmill", 10, 50, 17, true); // even an externally added fuel slot is untouched
        prefab.gameObject.component = prefab;
        var scene = new ZNetScene(); ZNetScene.instance = scene; scene.Registry["windmill"] = prefab.gameObject;
        var clone = Station("windmill(Clone)", 10, 50, 17, true);
        var other = Station("piece_spinningwheel(Clone)", 30, 10, 20, true);
        UnityEngine.Object.Loaded = new[] { clone, other };
        Plugin.Settings["windmill"] = new Plugin.StationSettings {Seconds=3, Input=250, Fuel=999};
        SmelterApplier.ApplyAll();
        Assert.Equal(3f, clone.m_secPerProduct); Assert.Equal(250, clone.m_maxOre); Assert.Equal(17, clone.m_maxFuel);
        Assert.Equal(30f, other.m_secPerProduct); Assert.Equal(10, other.m_maxOre);
        Plugin.Settings["windmill"] = new Plugin.StationSettings();
        SmelterApplier.ApplyAll();
        Assert.Equal(10f, clone.m_secPerProduct); Assert.Equal(50, clone.m_maxOre); Assert.Equal(17, clone.m_maxFuel);
    }

    [Fact]
    public void Missing_reports_wait_for_final_registration_pass_and_require_component()
    {
        Plugin.Messages.Clear();
        var scene = new ZNetScene();
        scene.Registry["smelter"] = new GameObject { name = "smelter" }; // wrong structure
        SmelterApplier.ApplyPrefabs(scene);
        Assert.DoesNotContain(Plugin.Messages, m => m.Contains("Missing"));
        SmelterApplier.ApplyPrefabs(scene, true);
        Assert.Equal(4, Plugin.Messages.Count(m => m.Contains("Missing")));
    }
}

// Minimal host doubles for exercising the unchanged production applier source.
namespace UnityEngine
{
    public enum FindObjectsSortMode { None }
    public enum FindObjectsInactive { Include }
    public class Object
    {
        public static Smelter[] Loaded = Array.Empty<Smelter>();
        public static bool IncludedInactive;
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort)
        { IncludedInactive = inactive == FindObjectsInactive.Include; return Loaded.Cast<T>().ToArray(); }
    }
    public class GameObject : Object
    {
        public string name;
        public Smelter component;
        public T GetComponent<T>() where T : class { return component as T; }
    }
}
public class ItemDrop { }
public class Smelter : UnityEngine.Object
{
    public GameObject gameObject;
    public float m_secPerProduct;
    public int m_maxOre, m_maxFuel;
    public ItemDrop m_fuelItem;
}
public class ZNetScene : UnityEngine.Object
{
    public static ZNetScene instance;
    public List<GameObject> m_prefabs = new List<GameObject>();
    public List<GameObject> m_nonNetViewPrefabs = new List<GameObject>();
    public Dictionary<string, GameObject> Registry = new Dictionary<string, GameObject>();
    public GameObject GetPrefab(string id) { Registry.TryGetValue(id, out var p); return p; }
}
namespace SmelterControl
{
    internal class Plugin
    {
        internal const string PluginGuid = "ghost.valheim.smeltercontrol";
        internal struct StationSettings { public float Seconds; public int Input, Fuel; }
        internal static Dictionary<string, StationSettings> Settings = new Dictionary<string, StationSettings>();
        internal static List<string> Messages = new List<string>();
        internal static StationSettings ConfigFor(string id) { return Settings[id]; }
        internal static void LogOnce(string message) { Messages.Add(message); }
        internal static void LogInfoOnce(string message) { }
    }
}
