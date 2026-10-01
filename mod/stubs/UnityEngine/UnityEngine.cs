// Compile-time stand-in for the parts of UnityEngine the mod uses, so the mod can be built without the game.
// At runtime the game's real UnityEngine.dll (which forwards these types to its module assemblies) is used.
// Build against the real game files (-p:GameDir=...) whenever possible.
#pragma warning disable 1591
using System;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }

        public static void DontDestroyOnLoad(Object target) { }
        public static void Destroy(Object obj) { }

        public static bool operator ==(Object x, Object y) { return ReferenceEquals(x, y); }
        public static bool operator !=(Object x, Object y) { return !ReferenceEquals(x, y); }
        public static implicit operator bool(Object exists) { return !ReferenceEquals(exists, null); }

        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return 0; }
    }

    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; }
    }

    public class MonoBehaviour : Behaviour
    {
    }

    public sealed class GameObject : Object
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : Component { return null; }
        public Component AddComponent(Type componentType) { return null; }
    }

    public static class Debug
    {
        public static void Log(object message)
        {
            if (Environment.GetEnvironmentVariable("LOBCORP_AP_ECHO") != null) Console.WriteLine(message);
        }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public static class Time
    {
        // A real clock so the stub can drive the mod in the offline test harness (tools/ModHarness).
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        public static float realtimeSinceStartup { get { return (float)Clock.Elapsed.TotalSeconds; } }
        public static float unscaledTime { get { return realtimeSinceStartup; } }
        public static float deltaTime { get { return 0.016f; } }
    }

    public static class Input
    {
        public static bool GetKeyDown(string name) { return false; }
        public static bool GetKey(string name) { return false; }
    }

    public static class Application
    {
        public static string persistentDataPath
        {
            get { return Environment.GetEnvironmentVariable("LOBCORP_AP_DATA") ?? Environment.CurrentDirectory; }
        }
        public static string dataPath { get { return ""; } }
    }

    public static class Screen
    {
        public static int width { get { return 0; } }
        public static int height { get { return 0; } }
    }

    public struct Rect
    {
        public Rect(float x, float y, float width, float height) { }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1f, 1f, 1f, 1f); } }
    }

    public enum TextAnchor
    {
        UpperLeft = 0,
        UpperCenter = 1,
        UpperRight = 2,
        MiddleLeft = 3,
        MiddleCenter = 4,
        MiddleRight = 5,
        LowerLeft = 6,
        LowerCenter = 7,
        LowerRight = 8
    }

    public sealed class GUIStyleState
    {
        public Color textColor { get; set; }
    }

    public sealed class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public TextAnchor alignment { get; set; }
        public int fontSize { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public GUIStyleState normal { get { return null; } set { } }
    }

    public sealed class GUISkin : Object
    {
        public GUIStyle box { get { return null; } set { } }
        public GUIStyle label { get { return null; } set { } }
    }

    public class GUI
    {
        public static GUISkin skin { get { return null; } set { } }
        public static Color color { get; set; }
        public static void Box(Rect position, string text) { }
        public static void Box(Rect position, string text, GUIStyle style) { }
        public static void Label(Rect position, string text) { }
        public static void Label(Rect position, string text, GUIStyle style) { }
    }
}
