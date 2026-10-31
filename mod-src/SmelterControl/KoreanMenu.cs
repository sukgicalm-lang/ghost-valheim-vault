using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace SmelterControl
{
    // Optional integration with the installed F1 manager. No dependency on its DLL.
    // Only display objects are changed: ConfigDefinition, ConfigDescription and the
    // owning plugins' metadata/config values are never replaced by localization.
    internal static class KoreanMenu
    {
        private static readonly Dictionary<string, BepInPlugin> DisplayPlugins = new Dictionary<string, BepInPlugin>();
        private static readonly Dictionary<ConfigEntryBase, Draft> Drafts = new Dictionary<ConfigEntryBase, Draft>();
        private static Type _managerType;
        private sealed class Draft { internal object Value; internal string Text, Error; }

        internal static void Install(Harmony harmony)
        {
            Type entryType = AccessTools.TypeByName("ConfigurationManager.ConfigSettingEntry");
            if (entryType == null) return;
            var constructor = entryType.GetConstructor(new[] { typeof(ConfigEntryBase), typeof(BaseUnityPlugin) });
            if (constructor == null) throw new MissingMethodException("F1 setting entry constructor");
            if (!MenuGui.Initialize()) throw new MissingMethodException("Unity IMGUI layout API");
            _managerType = AccessTools.TypeByName("ConfigurationManager.ConfigurationManager");
            harmony.Patch(constructor, postfix: new HarmonyMethod(typeof(KoreanMenu), nameof(Localize)));
            var collect = AccessTools.Method(AccessTools.TypeByName("ConfigurationManager.SettingSearcher"), "CollectSettings");
            if (collect != null) harmony.Patch(collect, postfix: new HarmonyMethod(typeof(KoreanMenu), nameof(LocalizeFixedNames)));
        }

        internal static void LocalizeFixedNames(ref List<string> __1)
        {
            if (__1 == null) return;
            for (int i = 0; i < __1.Count; i++)
                if (KoreanText.FixedNames.TryGetValue(__1[i], out string label)) __1[i] = label;
        }

        internal static void Refresh()
        {
            if (_managerType == null) return;
            object manager = AccessTools.Field(_managerType, "instance")?.GetValue(null);
            if (manager != null) AccessTools.Method(_managerType, "BuildSettingList", Type.EmptyTypes)?.Invoke(manager, null);
        }

        // Positional arguments work regardless of compiler-chosen parameter names.
        private static void Localize(object __instance, ConfigEntryBase __0, BaseUnityPlugin __1)
        {
            if (__0 == null || __1 == null) return;
            try { Apply(__instance, __0, __1.Info.Metadata); }
            catch (Exception ex) { Plugin.LogOnce("Korean F1 presentation unavailable: " + ex.Message); }
        }

        internal static bool Apply(object display, ConfigEntryBase entry, BepInPlugin plugin)
        {
            if (plugin == null || !KoreanText.TryGet(plugin.GUID, entry.Definition.Section, entry.Definition.Key, out KoreanSetting text)) return false;
            if (!DisplayPlugins.TryGetValue(plugin.GUID, out BepInPlugin header))
            {
                header = new BepInPlugin(plugin.GUID, KoreanText.Names[plugin.GUID], plugin.Version.ToString());
                DisplayPlugins.Add(plugin.GUID, header);
            }
            Type type = display.GetType();
            Put(type, display, "PluginInfo", header);
            Put(type, display, "Category", text.Category);
            Put(type, display, "DispName", text.Name);
            Put(type, display, "Description", text.Description);
            // Include the description inline, so users need not discover hover tooltips.
            Put(type, display, "HideDefaultButton", true);
            Put(type, display, "CustomDrawer", (Action<ConfigEntryBase>)(_ => Draw(display, entry, text)));
            return true;
        }

        private static void Put(Type type, object target, string name, object value)
        { type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value, null); }
        private static bool CanWrite(object display)
        { return !Equals(display.GetType().GetProperty("ReadOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(display, null), true); }

        private static void Draw(object display, ConfigEntryBase entry, KoreanSetting text)
        {
            MenuGui.BeginVertical();
            try
            {
                MenuGui.Label(text.Description);
                bool writable = CanWrite(display);
                if (text.Kind == "inactive") { MenuGui.Label("현재 버전에서는 이 값을 사용하지 않습니다."); return; }
                if (entry.SettingType == typeof(bool)) DrawBoolean(entry, writable);
                else if (entry.SettingType.IsEnum) DrawEnum(entry, text, writable);
                else if (text.Kind == "speed") DrawSpeed(entry, text, writable);
                else DrawValue(entry, text, writable);
                if (MenuGui.Button(ResetCaption(entry, text)) && writable)
                { entry.BoxedValue = entry.DefaultValue; Drafts.Remove(entry); }
                if (!writable) MenuGui.Label("서버 또는 모드에서 잠근 설정입니다. 여기서는 바꿀 수 없습니다.");
            }
            finally { MenuGui.EndVertical(); }
        }

        private static string ResetCaption(ConfigEntryBase entry, KoreanSetting text)
        {
            if (text.Kind == "speed")
            {
                SmelterApplier.TryBaselineSeconds(text.Station, out float baseline);
                return KoreanText.Positive(baseline) ? "모드 기본 속도로: " + KoreanText.SpeedLabel((float)entry.DefaultValue, baseline) : "모드 기본 설정으로 되돌리기";
            }
            object value = entry.DefaultValue;
            string caption = value is bool enabled ? (enabled ? "사용" : "사용 안 함")
                : value.GetType().IsEnum ? EnumLabel(value.ToString())
                : value is float number ? KoreanText.Number(ToDisplay(number, text.Kind))
                : Convert.ToString(value, CultureInfo.InvariantCulture);
            return "모드 기본값으로: " + caption + Unit(text.Kind);
        }

        private static void DrawBoolean(ConfigEntryBase entry, bool writable)
        {
            bool current = (bool)entry.BoxedValue;
            MenuGui.Label("현재: " + (current ? "사용" : "사용 안 함"));
            MenuGui.BeginHorizontal();
            try
            {
                if (MenuGui.Button(current ? "선택됨: 사용" : "사용") && writable && !current) entry.BoxedValue = true;
                if (MenuGui.Button(!current ? "선택됨: 사용 안 함" : "사용 안 함") && writable && current) entry.BoxedValue = false;
            }
            finally { MenuGui.EndHorizontal(); }
        }

        private static void DrawEnum(ConfigEntryBase entry, KoreanSetting text, bool writable)
        {
            MenuGui.Label("현재: " + EnumLabel(entry.BoxedValue.ToString()));
            foreach (object value in Enum.GetValues(entry.SettingType))
                if (MenuGui.Button((Equals(value, entry.BoxedValue) ? "선택됨: " : "") + EnumLabel(value.ToString())) && writable)
                    entry.BoxedValue = value;
        }
        internal static string EnumLabel(string name)
        {
            switch (name)
            {
                case "Valheim": return "발헤임 체형";
                case "Original": return "원작 체형";
                case "LeftAlt": return "왼쪽 Alt 키";
                case "LeftCtrl": case "LeftControl": return "왼쪽 Ctrl 키";
                case "LeftShift": return "왼쪽 Shift 키";
                default: return name;
            }
        }

        private static void DrawSpeed(ConfigEntryBase entry, KoreanSetting text, bool writable)
        {
            float seconds = (float)entry.BoxedValue;
            SmelterApplier.TryBaselineSeconds(text.Station, out float baseline);
            MenuGui.Label("현재: " + KoreanText.SpeedLabel(seconds, baseline));
            if (!KoreanText.Positive(baseline))
            {
                MenuGui.Label("월드에 들어가면 게임에서 원래 속도를 읽어 배수 버튼을 표시합니다. 기존 설정은 그대로 유지됩니다.");
                return;
            }
            MenuGui.BeginHorizontal();
            try
            {
                if (MenuGui.Button("원래 속도") && writable) { entry.BoxedValue = 0f; Drafts.Remove(entry); }
                foreach (float speed in new[] { 2f, 5f, 10f })
                    if (MenuGui.Button(KoreanText.Number(speed) + "배 빠르게") && writable && KoreanText.TrySeconds(baseline, speed, out float result))
                    { entry.BoxedValue = result; Drafts.Remove(entry); }
            }
            finally { MenuGui.EndHorizontal(); }
            seconds = (float)entry.BoxedValue;
            Draft draft = GetDraft(entry, KoreanText.Number(KoreanText.Speed(seconds, baseline)));
            MenuGui.Label("직접 입력 (배): 1은 원래 속도, 10은 약 10배 빠르게");
            MenuGui.BeginHorizontal();
            try
            {
                draft.Text = MenuGui.TextField(draft.Text);
                if (MenuGui.Button("배수 적용") && writable)
                {
                    if (draft.Text == KoreanText.Number(KoreanText.Speed(seconds, baseline))) { draft.Error = null; }
                    else if (TryNumber(draft.Text, out float speed) && KoreanText.TrySeconds(baseline, speed, out float result))
                    { entry.BoxedValue = result; Drafts.Remove(entry); }
                    else draft.Error = "0보다 큰 배수를 입력하세요. 예: 1, 2, 5, 10";
                }
            }
            finally { MenuGui.EndHorizontal(); }
            if (draft.Error != null) MenuGui.Label(draft.Error);
        }

        private static void DrawValue(ConfigEntryBase entry, KoreanSetting text, bool writable)
        {
            object current = entry.BoxedValue;
            string value = current is float f ? KoreanText.Number(ToDisplay(f, text.Kind)) : Convert.ToString(current, CultureInfo.InvariantCulture);
            string unit = Unit(text.Kind);
            MenuGui.Label("현재: " + (text.Kind == "count" && (int)current <= 0 ? "게임의 원래 보관량" : value + unit));
            Draft draft = GetDraft(entry, value);
            MenuGui.BeginHorizontal();
            try
            {
                draft.Text = MenuGui.TextField(draft.Text);
                if (MenuGui.Button("적용") && writable)
                {
                    // Applying an untouched rounded display must not alter stored precision.
                    if (draft.Text == value) { draft.Error = null; return; }
                    object next = null;
                    if (entry.SettingType == typeof(string)) next = draft.Text;
                    else if (entry.SettingType == typeof(int)) { if (int.TryParse(draft.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) next = count; }
                    else if (TryNumber(draft.Text, out float number))
                    {
                        float raw = FromDisplay(number, text.Kind);
                        if (!float.IsNaN(raw) && !float.IsInfinity(raw) && (text.Kind != "minutes" || raw >= 1f)) next = raw;
                    }
                    if (next != null && (entry.Description.AcceptableValues == null || entry.Description.AcceptableValues.IsValid(next)))
                    { entry.BoxedValue = next; Drafts.Remove(entry); }
                    else draft.Error = "설명에 적힌 범위의 숫자를 입력하세요. 현재 설정은 유지됩니다.";
                }
            }
            finally { MenuGui.EndHorizontal(); }
            if (draft.Error != null) MenuGui.Label(draft.Error);
        }
        private static string Unit(string kind)
        { return kind == "minutes" ? "분" : kind == "percent" ? "%" : kind == "count" ? "개" : kind == "ratio" || kind == "xp" ? "배" : ""; }
        internal static float ToDisplay(float raw, string kind) { return kind == "minutes" ? raw / 60f : kind == "percent" ? raw * 100f : raw; }
        internal static float FromDisplay(float shown, string kind) { return kind == "minutes" ? shown * 60f : kind == "percent" ? shown / 100f : shown; }
        internal static bool TryNumber(string input, out float value)
        { return float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value); }
        private static Draft GetDraft(ConfigEntryBase entry, string current)
        {
            if (!Drafts.TryGetValue(entry, out Draft draft) || !Equals(draft.Value, entry.BoxedValue))
            { draft = new Draft { Value = entry.BoxedValue, Text = current }; Drafts[entry] = draft; }
            return draft;
        }
    }
}
