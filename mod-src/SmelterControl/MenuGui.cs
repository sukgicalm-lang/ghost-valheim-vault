using System;
using System.Reflection;

namespace SmelterControl
{
    // Resolve IMGUI from the running game. No bundled/older Unity module is loaded.
    // All lookups are cached, and the bridge runs only while an F1 entry is drawn.
    internal static class MenuGui
    {
        private static MethodInfo _vertical, _horizontal, _endVertical, _endHorizontal, _button, _text, _label;
        private static PropertyInfo _skin, _skinLabel, _wrap, _fontSize;
        private static ConstructorInfo _styleConstructor;
        private static object _options;
        private static object _labelStyle, _sourceStyle;
        private static int _labelFontSize;
        internal static bool Initialize()
        {
            Type layout = Type.GetType("UnityEngine.GUILayout, UnityEngine.IMGUIModule");
            Type option = Type.GetType("UnityEngine.GUILayoutOption, UnityEngine.IMGUIModule");
            Type style = Type.GetType("UnityEngine.GUIStyle, UnityEngine.IMGUIModule");
            Type gui = Type.GetType("UnityEngine.GUI, UnityEngine.IMGUIModule");
            Type skin = Type.GetType("UnityEngine.GUISkin, UnityEngine.IMGUIModule");
            if (layout == null || option == null || style == null || gui == null || skin == null) return false;
            Type options = option.MakeArrayType();
            _options = Array.CreateInstance(option, 0);
            _vertical = layout.GetMethod("BeginVertical", new[] { options });
            _horizontal = layout.GetMethod("BeginHorizontal", new[] { options });
            _endVertical = layout.GetMethod("EndVertical", Type.EmptyTypes);
            _endHorizontal = layout.GetMethod("EndHorizontal", Type.EmptyTypes);
            _button = layout.GetMethod("Button", new[] { typeof(string), options });
            _text = layout.GetMethod("TextField", new[] { typeof(string), options });
            _label = layout.GetMethod("Label", new[] { typeof(string), style, options });
            _skin = gui.GetProperty("skin"); _skinLabel = skin.GetProperty("label"); _wrap = style.GetProperty("wordWrap");
            _styleConstructor = style.GetConstructor(new[] { style });
            _fontSize = style.GetProperty("fontSize");
            return _vertical != null && _horizontal != null && _endVertical != null && _endHorizontal != null
                && _button != null && _text != null && _label != null && _skin != null && _skinLabel != null && _wrap != null && _styleConstructor != null && _fontSize != null;
        }
        internal static void BeginVertical() { _vertical.Invoke(null, new[] { _options }); }
        internal static void BeginHorizontal() { _horizontal.Invoke(null, new[] { _options }); }
        internal static void EndVertical() { _endVertical.Invoke(null, null); }
        internal static void EndHorizontal() { _endHorizontal.Invoke(null, null); }
        internal static bool Button(string text) { return (bool)_button.Invoke(null, new[] { text, _options }); }
        internal static string TextField(string text) { return (string)_text.Invoke(null, new[] { text, _options }); }
        internal static void Label(string text)
        {
            // Build from the current manager skin so its font/scale/theme is respected.
            object source = _skinLabel.GetValue(_skin.GetValue(null, null), null);
            int fontSize = (int)_fontSize.GetValue(source, null);
            if (!ReferenceEquals(source, _sourceStyle) || _labelStyle == null || fontSize != _labelFontSize)
            {
                _labelStyle = _styleConstructor.Invoke(new[] { source });
                _wrap.SetValue(_labelStyle, true, null);
                _sourceStyle = source; _labelFontSize = fontSize;
            }
            _label.Invoke(null, new[] { text, _labelStyle, _options });
        }
    }
}
