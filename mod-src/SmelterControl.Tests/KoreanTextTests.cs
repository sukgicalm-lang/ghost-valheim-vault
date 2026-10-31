using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmelterControl;
using Xunit;

public class KoreanTextTests
{
    private static string Root => Environment.GetEnvironmentVariable("FRIZZ_WORKSPACE_ROOT");
    [Fact]
    public void Every_bound_setting_in_the_actual_owned_plugins_has_Korean_presentation()
    {
        var files = new[] {
            "references/suite/BepInEx/plugins/Ghost-GhostDoomLight/GhostDoomLight.dll",
            "references/suite/BepInEx/plugins/Ghost-GhostValheimTweaks/GhostValheimTweaks.dll",
            "references/suite/BepInEx/plugins/Landoria-MapTeleport/Landoria.MapTeleport.dll",
            "upstream/SmelterControl/bin/Release/net48/GhostSmelterControl.dll" };
        var actual = new HashSet<string>();
        foreach (string path in files)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(Root, path));
            var type = assembly.MainModule.Types.Single(t => t.CustomAttributes.Any(a => a.AttributeType.Name == "BepInPlugin"));
            string guid = (string)type.CustomAttributes.Single(a => a.AttributeType.Name == "BepInPlugin").ConstructorArguments[0].Value;
            var code = type.Methods.Single(m => m.Name == "Awake").Body.Instructions;
            for (int i = 0; i < code.Count; i++)
            {
                if (!(code[i].Operand is MethodReference call) || call.Name != "Bind") continue;
                if (code[i + 1].OpCode != OpCodes.Stsfld && !(code[i + 1].Operand is MethodReference setter && setter.Name.StartsWith("set_"))) continue;
                int start = i;
                while (start > 0 && !(code[start].Operand is MethodReference m && m.Name == "get_Config")) start--;
                var strings = code.Skip(start).Take(i - start).Where(x => x.OpCode == OpCodes.Ldstr).Select(x => (string)x.Operand).ToArray();
                Assert.True(strings.Length >= 2);
                string id = KoreanText.Id(guid, strings[0], strings[1]); actual.Add(id);
                Assert.True(KoreanText.Settings.TryGetValue(id, out var text), id);
                Assert.True(text.Name.Any(c => c >= '가' && c <= '힣'), id);
                Assert.True(text.Description.Length > 30, id);
                Assert.True(text.Category.Any(c => c >= '가' && c <= '힣'), id);
            }
        }
        Assert.Equal(40, actual.Count);
        Assert.Equal(actual.OrderBy(s => s), KoreanText.Settings.Keys.OrderBy(s => s));
    }

    [Fact]
    public void Third_party_and_unknown_keys_are_not_localized()
    {
        Assert.False(KoreanText.TryGet("shudnal.ConfigurationManager", "General", "Enabled", out _));
        Assert.False(KoreanText.TryGet("unrelated.plugin", "Smelter", "Seconds", out _));
        Assert.False(KoreanText.TryGet(KoreanText.Wardrobe, "General", "NewUnknownSetting", out _));
        Assert.Equal(4, KoreanText.Names.Count);
    }

    [Theory]
    [InlineData(10f, 1f, 10f)]
    [InlineData(10f, 2f, 5f)]
    [InlineData(10f, 5f, 2f)]
    [InlineData(10f, 10f, 1f)]
    [InlineData(30f, 3f, 10f)]
    [InlineData(15f, 1.5f, 10f)]
    public void Speed_uses_captured_baseline_instead_of_assuming_ten_seconds(float baseline, float seconds, float speed)
    {
        Assert.Equal(speed, KoreanText.Speed(seconds, baseline));
        Assert.True(KoreanText.TrySeconds(baseline, speed, out float roundtrip));
        Assert.Equal(seconds, roundtrip);
    }

    [Fact]
    public void Missing_baseline_invalid_values_and_restore_are_explained_safely()
    {
        Assert.Contains("월드", KoreanText.SpeedLabel(1, 0));
        Assert.Equal("원래 속도 (1배)", KoreanText.SpeedLabel(0, 10));
        Assert.Equal("원래 속도 (1배)", KoreanText.SpeedLabel(-5, 10));
        Assert.Equal("원래 속도 (1배)", KoreanText.SpeedLabel(float.NaN, 10));
        foreach (float invalid in new[] {0f, -1f, float.NaN, float.PositiveInfinity})
        { Assert.False(KoreanText.TrySeconds(10, invalid, out _)); Assert.False(KoreanText.TrySeconds(invalid, 10, out _)); }
    }

    [Fact]
    public void Installed_manager_constructor_and_display_setters_match_the_optional_hook()
    {
        using var cm = AssemblyDefinition.ReadAssembly(Path.Combine(Root, "references/configmanager/ConfigurationManager.dll"));
        Assert.Equal(new Version(1,1,27,0), cm.Name.Version);
        var entry = cm.MainModule.GetType("ConfigurationManager.ConfigSettingEntry");
        Assert.Contains(entry.Methods, m => m.IsConstructor && m.Parameters.Count == 2
            && m.Parameters[0].ParameterType.FullName == "BepInEx.Configuration.ConfigEntryBase"
            && m.Parameters[1].ParameterType.FullName == "BepInEx.BaseUnityPlugin");
        var display = cm.MainModule.GetType("ConfigurationManager.SettingEntryBase");
        foreach (string name in new[] {"PluginInfo", "Category", "DispName", "Description", "CustomDrawer", "HideDefaultButton"})
            Assert.NotNull(display.Properties.Single(p => p.Name == name).SetMethod);
        Assert.NotNull(display.Properties.Single(p => p.Name == "ReadOnly").GetMethod);
    }
}
