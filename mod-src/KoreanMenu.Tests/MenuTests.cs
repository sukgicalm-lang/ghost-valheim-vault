using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using SmelterControl;
using UnityEngine;
using Xunit;

public class DisplayBase
{
    public BepInPlugin PluginInfo { get; private set; }
    public string Category { get; private set; }
    public string DispName { get; private set; }
    public string Description { get; private set; }
    public bool HideDefaultButton { get; private set; }
    public bool? ReadOnly { get; set; }
    public Action<ConfigEntryBase> CustomDrawer { get; private set; }
}
public class Display : DisplayBase { }
public enum Shape { Valheim, Original }
public enum Keys { LeftAlt, LeftCtrl, LeftShift }
public class MenuTests
{
    private static ConfigFile FileConfig() { return new ConfigFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".cfg"), false); }
    private static Display Localize(ConfigEntryBase entry, string guid = Plugin.PluginGuid)
    {
        Assert.True(MenuGui.Initialize());
        var view = new Display(); Assert.True(KoreanMenu.Apply(view, entry, new BepInPlugin(guid,"Original name","1.0.0"))); return view;
    }
    private static void Draw(Display view, ConfigEntryBase entry, string click = null, string input = null)
    {
        GUILayout.Reset(); GUILayout.Click = click; GUILayout.Input = input;
        view.CustomDrawer(entry); Assert.Equal(0, GUILayout.Depth);
    }
    [Fact]
    public void Opening_localized_menu_preserves_keys_values_and_original_metadata_and_groups_settings_once()
    {
        var cfg=FileConfig(); var speed=cfg.Bind("Windmill","Seconds",2f,"Original tooltip");
        var count=cfg.Bind("Windmill","Barley",100,"Capacity");
        string serialized=speed.GetSerializedValue(); var a=Localize(speed); var b=Localize(count);
        Assert.Same(a.PluginInfo,b.PluginInfo); Assert.Equal("Ghost 제련·제분 설정",a.PluginInfo.Name);
        Assert.Equal("Windmill",speed.Definition.Section); Assert.Equal("Seconds",speed.Definition.Key);
        Assert.Equal("Original tooltip",speed.Description.Description);
        Draw(a,speed); Assert.Equal(serialized,speed.GetSerializedValue());
        Assert.Contains("현재: 약 5배 속도",GUILayout.Labels);
        Assert.Contains(a.Description,GUILayout.Labels);
        Assert.Equal("5",GUILayout.LastInput);
    }
    [Fact]
    public void Speed_buttons_direct_entry_restore_and_mod_default_convert_without_changing_saved_schema()
    {
        var speed=FileConfig().Bind("Windmill","Seconds",1f,""); speed.Value=2f; var view=Localize(speed);
        Draw(view,speed,"10배 빠르게"); Assert.Equal(1f,speed.Value); Assert.Equal("10",GUILayout.LastInput);
        Draw(view,speed,"배수 적용","2.5"); Assert.Equal(4f,speed.Value);
        Draw(view,speed,"원래 속도"); Assert.Equal(0f,speed.Value);
        Draw(view,speed,"모드 기본 속도로: 약 10배 속도"); Assert.Equal(1f,speed.Value);
        Draw(view,speed,"배수 적용","NaN"); Assert.Equal(1f,speed.Value);
        Draw(view,speed,"배수 적용","0"); Assert.Equal(1f,speed.Value);
        Draw(view,speed,"배수 적용","-2"); Assert.Equal(1f,speed.Value);
        Assert.Equal("Seconds",speed.Definition.Key);
    }
    [Fact]
    public void Unknown_baseline_does_not_guess_a_multiplier_or_reset_existing_value()
    {
        var speed=FileConfig().Bind("Smelter","Seconds",2f,""); var view=Localize(speed);
        Draw(view,speed,"10배 빠르게"); Assert.Equal(2f,speed.Value);
        Assert.Contains("현재: 월드 입장 후 원래 속도와 비교합니다",GUILayout.Labels);
        Assert.Null(GUILayout.LastInput);
    }
    [Fact]
    public void Locked_settings_cannot_be_changed_even_when_manager_uses_colored_readonly_style()
    {
        var cfg=FileConfig();var speed=cfg.Bind("Windmill","Seconds",2f,"");var v=Localize(speed);v.ReadOnly=true;
        Draw(v,speed,"10배 빠르게");Assert.Equal(2f,speed.Value);
        Draw(v,speed,"원래 속도");Assert.Equal(2f,speed.Value);
        Draw(v,speed,"배수 적용","20");Assert.Equal(2f,speed.Value);
        var enabled=cfg.Bind("General","Enabled",false,"");var b=Localize(enabled,KoreanText.Wardrobe);b.ReadOnly=true;
        Draw(b,enabled,"사용");Assert.False(enabled.Value);
    }
    [Fact]
    public void Minutes_percent_ranges_and_untouched_precision_are_preserved()
    {
        var cfg=FileConfig();var minutes=cfg.Bind("1 - 발효 시간","발효 시간(초)",333.33334f,"");var m=Localize(minutes,KoreanText.Tweaks);
        Draw(m,minutes,"적용");Assert.Equal(333.33334f,minutes.Value);
        Draw(m,minutes,"적용","5");Assert.Equal(300f,minutes.Value);
        Draw(m,minutes,"적용","0");Assert.Equal(300f,minutes.Value);
        var opacity=cfg.Bind("Tyrael Wings","TyraelWingOpacity",.48f,new ConfigDescription("",new AcceptableValueRange<float>(.15f,1f)));
        var p=Localize(opacity,KoreanText.Wardrobe);Draw(p,opacity);Assert.Equal("48",GUILayout.LastInput);
        Draw(p,opacity,"적용","60");Assert.Equal(.6f,opacity.Value);
        Draw(p,opacity,"적용","101");Assert.Equal(.6f,opacity.Value);
        var speed=cfg.Bind("Windmill","Seconds",1.2345678f,"");var s=Localize(speed);Draw(s,speed,"배수 적용");Assert.Equal(1.2345678f,speed.Value);
    }
    [Fact]
    public void Capacity_is_a_count_and_zero_explicitly_restores_game_capacity()
    {
        var entry=FileConfig().Bind("Windmill","Barley",100,"");var view=Localize(entry);
        Draw(view,entry,"적용","250");Assert.Equal(250,entry.Value);
        Draw(view,entry,"적용","0");Draw(view,entry);Assert.Contains("현재: 게임의 원래 보관량",GUILayout.Labels);
        Draw(view,entry,"적용","1.5");Assert.Equal(0,entry.Value);
        Draw(view,entry,"모드 기본값으로: 100개");Assert.Equal(100,entry.Value);
    }
    [Fact]
    public void Boolean_and_enum_controls_show_Korean_choices_and_unused_legacy_fields_are_explained()
    {
        var cfg=FileConfig();var flag=cfg.Bind("General","Enabled",true,"");var b=Localize(flag,KoreanText.Wardrobe);
        Draw(b,flag,"사용 안 함");Assert.False(flag.Value);
        var shape=cfg.Bind("Appearance","BodyShape",Shape.Valheim,"");var s=Localize(shape,KoreanText.Wardrobe);
        Draw(s,shape,"원작 체형");Assert.Equal(Shape.Original,shape.Value);
        var key=cfg.Bind("Controls","TeleportKey",Keys.LeftAlt,"");var k=Localize(key,KoreanText.Map);
        Draw(k,key,"왼쪽 Shift 키");Assert.Equal(Keys.LeftShift,key.Value);
        var old=cfg.Bind("Appearance","OriginalScaleDoom",4.5f,"");var o=Localize(old,KoreanText.Wardrobe);
        Draw(o,old,"적용","8");Assert.Equal(4.5f,old.Value);Assert.Contains("현재 버전에서는 이 값을 사용하지 않습니다.",GUILayout.Labels);
    }
    [Fact]
    public void Third_party_display_is_completely_unchanged()
    {
        var entry=FileConfig().Bind("General","Enabled",true,"");var view=new Display();
        Assert.False(KoreanMenu.Apply(view,entry,new BepInPlugin("third.party","Original","1.0.0")));
        Assert.Null(view.DispName);Assert.Null(view.CustomDrawer);Assert.Null(view.PluginInfo);
    }
    [Fact]
    public void Fixed_bow_modules_have_Korean_names_without_adding_controls()
    {
        var names=new List<string>{"Ghost Bow Draw","Ghost Bow Skill","Third-party Mod"};
        KoreanMenu.LocalizeFixedNames(ref names);
        Assert.Contains("2배 고정",names[0]);Assert.Contains("9발",names[1]);Assert.Equal("Third-party Mod",names[2]);
        Assert.Equal(40,KoreanText.Settings.Count);
    }
}
namespace SmelterControl
{
    internal static class Plugin
    {
        internal const string PluginGuid="ghost.valheim.smeltercontrol";
        internal static void LogOnce(string text) { }
    }
    internal static class SmelterApplier
    {
        internal static bool TryBaselineSeconds(string station,out float seconds)
        { seconds=station=="windmill"?10:0;return seconds>0; }
    }
}
