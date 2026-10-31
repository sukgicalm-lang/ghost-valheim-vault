// Test-only IMGUI API. No Unity rendering or native code is run.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class GUILayoutOption { }
    public class GUIStyle
    {
        public bool wordWrap { get; set; }
        public int fontSize { get; set; } = 18;
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { fontSize = other.fontSize; wordWrap = other.wordWrap; }
    }
    public class GUISkin { public GUIStyle label { get; set; } = new GUIStyle(); }
    public static class GUI { public static GUISkin skin { get; set; } = new GUISkin(); }
    public static class GUILayout
    {
        public static string Click, Input, LastInput;
        public static List<string> Labels = new List<string>();
        public static int Depth;
        public static void BeginVertical(params GUILayoutOption[] options) { Depth++; }
        public static void BeginHorizontal(params GUILayoutOption[] options) { Depth++; }
        public static void EndVertical() { Depth--; }
        public static void EndHorizontal() { Depth--; }
        public static bool Button(string text, params GUILayoutOption[] options)
        { if (Click == text) { Click = null; return true; } return false; }
        public static string TextField(string text, params GUILayoutOption[] options)
        { LastInput = Input ?? text; Input = null; return LastInput; }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options)
        { if (!style.wordWrap) throw new Exception("Descriptions must wrap"); Labels.Add(text); }
        public static void Reset() { Click = Input = LastInput = null; Labels.Clear(); Depth = 0; }
    }
}
