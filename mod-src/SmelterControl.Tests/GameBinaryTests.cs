using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

// These checks read the REAL supplied game DLL and the shipped plugin DLL.
// They do not execute Unity or stand in for an in-game smoke test.
public class GameBinaryTests
{
    private static string Root => Environment.GetEnvironmentVariable("FRIZZ_WORKSPACE_ROOT")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static AssemblyDefinition Game() => AssemblyDefinition.ReadAssembly(
        Path.Combine(Root, "references/Game/assembly_valheim.dll"));
    private static AssemblyDefinition Plugin() => AssemblyDefinition.ReadAssembly(
        Path.Combine(Root, "upstream/SmelterControl/bin/Release/net48/GhostSmelterControl.dll"));

    [Fact]
    public void Supplied_game_binary_is_1_0_20()
    {
        using var game = Game();
        var instructions = game.MainModule.GetType("Version").Methods.Single(m => m.Name == ".cctor").Body.Instructions;
        int i = instructions.ToList().FindIndex(x => x.OpCode == OpCodes.Newobj
            && ((MethodReference)x.Operand).DeclaringType.Name == "GameVersion");
        Assert.True(i >= 3);
        Assert.Equal(OpCodes.Ldc_I4_1, instructions[i - 3].OpCode);
        Assert.Equal(OpCodes.Ldc_I4_0, instructions[i - 2].OpCode);
        Assert.Equal(20, Convert.ToInt32(instructions[i - 1].Operand));
    }

    [Fact]
    public void Smelter_fields_and_rpc_signatures_match_game()
    {
        using var game = Game(); var t = game.MainModule.GetType("Smelter");
        foreach (var pair in new[] { ("m_secPerProduct", "System.Single"),
            ("m_maxOre", "System.Int32"), ("m_maxFuel", "System.Int32"),
            ("m_fuelPerProduct", "System.Int32"), ("m_fuelItem", "ItemDrop") })
        {
            var f = t.Fields.Single(x => x.Name == pair.Item1);
            Assert.True(f.IsPublic); Assert.Equal(pair.Item2, f.FieldType.FullName);
        }
        Assert.Equal("ZNetView", t.Fields.Single(f => f.Name == "m_nview").FieldType.FullName);
        Assert.Equal("System.Int32", t.Methods.Single(m => m.Name == "GetQueueSize").ReturnType.FullName);
        Assert.Equal("System.Single", t.Methods.Single(m => m.Name == "GetFuel").ReturnType.FullName);
        Assert.Equal(new[] { "System.Int64", "System.String", "System.Boolean" },
            t.Methods.Single(m => m.Name == "RPC_AddOre").Parameters.Select(p => p.ParameterType.FullName));
        Assert.Equal(new[] { "System.Int64" },
            t.Methods.Single(m => m.Name == "RPC_AddFuel").Parameters.Select(p => p.ParameterType.FullName));
        Assert.Single(t.Methods, m => m.Name == "Awake");
        var timer = t.Methods.Single(m => m.Name == "UpdateSmelter").Body.Instructions;
        Assert.Contains(timer, i => i.Operand is FieldReference f && f.Name == "m_secPerProduct");
        var awake = t.Methods.Single(m => m.Name == "Awake").Body.Instructions;
        int index = awake.ToList().FindIndex(i => (i.Operand as string) == "UpdateSmelter");
        Assert.Equal(1f, awake[index + 1].Operand); Assert.Equal(1f, awake[index + 2].Operand);
    }

    [Fact]
    public void Scene_has_two_prefab_lists_hash_lookup_and_no_Start()
    {
        using var game = Game(); var t = game.MainModule.GetType("ZNetScene");
        foreach (string name in new[] { "m_prefabs", "m_nonNetViewPrefabs" })
        {
            var f = t.Fields.Single(x => x.Name == name);
            Assert.True(f.IsPublic);
            Assert.Equal("System.Collections.Generic.List`1<UnityEngine.GameObject>", f.FieldType.FullName);
        }
        Assert.Contains(t.Methods, m => m.IsPublic && m.Name == "GetPrefab" && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName == "System.String");
        var fields = t.Methods.Single(m => m.Name == "Awake").Body.Instructions
            .Select(i => i.Operand).OfType<FieldReference>().Select(f => f.Name).ToArray();
        Assert.Contains("m_prefabs", fields); Assert.Contains("m_nonNetViewPrefabs", fields);
        Assert.Contains("m_namedPrefabs", fields);
        Assert.DoesNotContain(t.Methods, m => m.Name == "Start");
    }

    [Fact]
    public void Real_windmill_uses_root_smelter_and_native_wind_multiplier_is_preserved()
    {
        using var game = Game(); using var plugin = Plugin();
        var windmill = game.MainModule.GetType("Windmill");
        var start = windmill.Methods.Single(m => m.Name == "Start").Body.Instructions;
        Assert.Contains(start, i => i.Operand is GenericInstanceMethod m && m.Name == "GetComponent" && m.GenericArguments.Single().Name == "Smelter");
        var smelter = game.MainModule.GetType("Smelter");
        Assert.Equal("Windmill", smelter.Fields.Single(f => f.Name == "m_windmill").FieldType.Name);
        var tick = smelter.Methods.Single(m => m.Name == "UpdateSmelter").Body.Instructions;
        Assert.Contains(tick, i => i.Operand is MethodReference m && m.DeclaringType.Name == "Windmill" && m.Name == "GetPowerOutput");
        var power = windmill.Methods.Single(m => m.Name == "GetPowerOutput").Body.Instructions;
        Assert.Contains(power, i => i.Operand is MethodReference m && m.Name == "GetWindIntensity");
        Assert.Contains(power, i => i.Operand is FieldReference f && f.Name == "m_cover");
        var pluginMethods = plugin.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody);
        Assert.DoesNotContain(pluginMethods.SelectMany(m => m.Body.Instructions), i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && (f.DeclaringType.Name == "Windmill" || f.Name == "m_windmill"));
        Assert.DoesNotContain(plugin.MainModule.Types.SelectMany(t => t.CustomAttributes).Where(a => a.AttributeType.Name == "HarmonyPatch"), a => ((TypeReference)a.ConstructorArguments[0].Value).Name == "Windmill");
    }

    [Fact]
    public void Shipped_dll_writes_only_three_smelter_fields_and_patches_only_existing_methods()
    {
        using var game = Game(); using var plugin = Plugin();
        var methods = plugin.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();
        var writes = methods.SelectMany(m => m.Body.Instructions)
            .Where(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.Name == "Smelter")
            .Select(i => ((FieldReference)i.Operand).Name).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "m_maxFuel", "m_maxOre", "m_secPerProduct" }, writes);
        var patches = plugin.MainModule.Types.SelectMany(t => t.CustomAttributes)
            .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").ToArray();
        Assert.Equal(4, patches.Length);
        foreach (var a in patches)
        {
            var type = game.MainModule.GetType(((TypeReference)a.ConstructorArguments[0].Value).FullName);
            Assert.NotNull(type);
            Assert.Single(type.Methods, m => m.Name == (string)a.ConstructorArguments[1].Value);
            Assert.Contains(type.Name, new[] { "Smelter", "ZNetScene" });
        }
        Assert.Equal(new System.Version(1, 0, 1, 0), plugin.Name.Version);
        var attr = plugin.MainModule.GetType("SmelterControl.Plugin").CustomAttributes
            .Single(a => a.AttributeType.Name == "BepInPlugin");
        Assert.Equal("ghost.valheim.smeltercontrol", attr.ConstructorArguments[0].Value);
        Assert.Equal("1.0.1", attr.ConstructorArguments[2].Value);
        var incompatible = plugin.MainModule.GetType("SmelterControl.Plugin").CustomAttributes
            .Single(a => a.AttributeType.Name == "BepInIncompatibility");
        Assert.Equal("com.frizzqol.smeltercontrol", incompatible.ConstructorArguments[0].Value);
    }

    [Fact]
    public void F1_config_keys_preserved_and_compiled_defaults_are_one_second_and_100()
    {
        using var plugin = Plugin();
        var instructions = plugin.MainModule.GetType("SmelterControl.Plugin")
            .Methods.Single(m => m.Name == "Awake").Body.Instructions;
        var bindings = instructions.Where(i => i.Operand is MethodReference m
            && m.Name == "Bind" && m.DeclaringType.FullName == "BepInEx.Configuration.ConfigFile").ToArray();
        Assert.Equal(10, bindings.Length);
        foreach (var call in bindings)
        {
            int i = instructions.IndexOf(call);
            string section = (string)instructions[i - 4].Operand;
            string key = (string)instructions[i - 3].Operand;
            Assert.Contains(section + "/" + key, new[] { "Smelter/Seconds", "Smelter/Ore", "Smelter/Fuel",
                "BlastFurnace/Seconds", "BlastFurnace/Ore", "BlastFurnace/Fuel", "CharcoalKiln/Seconds", "CharcoalKiln/Wood", "Windmill/Seconds", "Windmill/Barley" });
            Assert.Equal(key == "Seconds" ? 1f : 100f, Convert.ToSingle(instructions[i - 2].Operand));
        }
    }
}
