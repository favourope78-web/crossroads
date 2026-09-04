// ============================================================================
// DEV-ONLY minimal UnityEngine/UnityEngine.UI stub.
// Lets the sandbox compile-check the CROSSROADS C# (mcs) and run the headless
// decision-system tests. NOT part of the Unity project (outside Assets/) and
// never referenced by game code - game code only uses real Unity APIs.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;

#pragma warning disable 0067, 0108, 0649, 0114, 0660, 0661

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { }
        public static T FindObjectOfType<T>() where T : Object { return null; }
        public static T FindFirstObjectByType<T>() where T : Object { return null; }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object { return new T[0]; }
        public static T Instantiate<T>(T o) where T : Object { return o; }
        public override string ToString() { return name; }
        public static implicit operator bool(Object o) { return o != null; }
    }

    public enum FindObjectsSortMode { None, InstanceID }

    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
        public Transform transform { get { return null; } }
        public bool enabled { get; set; }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; }
    }

    public class Behaviour : Component
    {
        public bool isActiveAndEnabled { get { return enabled; } set { enabled = value; } }
    }

    public class ScriptableObject : Object { }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) { return null; }
        public void StopCoroutine(Coroutine routine) { }
        public void StopAllCoroutines() { }
        public void Invoke(string method, float time) { }
        public void CancelInvoke() { }
        public void CancelInvoke(string method) { }
    }

    public class Coroutine { }

    public class YieldInstruction { }
    public class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }
    public class WaitForSecondsRealtime : YieldInstruction { public WaitForSecondsRealtime(float s) { } }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class GameObject : Object
    {
        public Transform transform { get { return null; } }
        public int layer;
        public static GameObject CreatePrimitive(PrimitiveType type) { return new GameObject(type.ToString()); }
        public string tag = "Untagged";
        public bool activeSelf { get; private set; }
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>() { return default(T); }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
        public void SetActive(bool v) { activeSelf = v; }
        public static GameObject FindGameObjectWithTag(string t) { return null; }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 right { get { return Vector3.right; } }
        public Quaternion rotation;
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Transform parent { get; set; }
        public int childCount;
        public void SetParent(Transform p, bool worldPositionStays) { parent = p; }
        public void SetPositionAndRotation(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
        public void Rotate(float x, float y, float z) { }
        public Vector3 forward { get { return Vector3.forward; } }
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, pivot, offsetMin, offsetMax, sizeDelta, anchoredPosition;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public Vector2 normalized { get { return this; } }
        public float sqrMagnitude { get { return x * x + y * y; } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator /(Vector2 a, float d) { return new Vector2(a.x / d, a.y / d); }
        public static bool operator ==(Vector2 a, Vector2 b) { return a.x == b.x && a.y == b.y; }
        public static bool operator !=(Vector2 a, Vector2 b) { return !(a == b); }
        public override bool Equals(object o) { return o is Vector2 && this == (Vector2)o; }
        public override int GetHashCode() { return x.GetHashCode() ^ y.GetHashCode(); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static readonly Vector3 zero = new Vector3(0, 0, 0);
        public static readonly Vector3 one = new Vector3(1, 1, 1);
        public static readonly Vector3 forward = new Vector3(0, 0, 1);
        public static readonly Vector3 up = new Vector3(0, 1, 0);
        public static readonly Vector3 down = new Vector3(0, -1, 0);
        public static readonly Vector3 right = new Vector3(1, 0, 0);
        public Vector3 normalized { get { return this; } }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float d) { return new Vector3(a.x * d, a.y * d, a.z * d); }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { return a + (b - a) * t; }
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 d = target - current;
            float dist = d.magnitude;
            if (dist <= maxDelta || dist < 1e-6f) return target;
            return current + d * (maxDelta / dist);
        }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 vel, float smoothTime) { return target; }
        public override string ToString() { return "(" + x + ", " + y + ", " + z + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion { w = 1 }; } }
        public static Quaternion Euler(float x, float y, float z) { return identity; }
        public static Quaternion LookRotation(Vector3 dir) { return identity; }
        public static Quaternion LookRotation(Vector3 dir, Vector3 up) { return identity; }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) { return b; }
        public static Vector3 operator *(Quaternion q, Vector3 v) { return v; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public static Color white { get { return new Color(1, 1, 1, 1); } }
        public static Color black { get { return new Color(0, 0, 0, 1); } }
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a * f); }
    }

    public struct Rect
    {
        public Vector2 position, size;
        public Rect(float x, float y, float w, float h) { position = new Vector2(x, y); size = new Vector2(w, h); }
        public static bool operator ==(Rect a, Rect b) { return a.position == b.position && a.size == b.size; }
        public static bool operator !=(Rect a, Rect b) { return !(a == b); }
        public override bool Equals(object o) { return o is Rect && this == (Rect)o; }
        public override int GetHashCode() { return position.GetHashCode() ^ size.GetHashCode(); }
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Font : Object { }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object { return null; }
    }

    public class Sprite : Object { }

    public static class Debug
    {
        public static void Log(object o) { Console.WriteLine("[LOG] " + o); }
        public static void LogWarning(object o) { Console.WriteLine("[WARN] " + o); }
        public static void LogError(object o) { Console.WriteLine("[ERR] " + o); }
    }

    public static class Application
    {
        public static string persistentDataPath = ".";
    }

    public static class Time
    {
        public static float time { get { return 0f; } }
        public static float deltaTime { get { return 0.016f; } }
        public static float unscaledTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0.016f; } }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float SmoothDampAngle(float a, float b, ref float v, float t) { return b; }
        public static float SmoothStep(float a, float b, float t) { return t; }
        public static float InverseLerp(float a, float b, float v) { return (v - a) / (b - a); }
        public static float DeltaAngle(float a, float b) { return b - a; }
        public static float Abs(float a) { return Math.Abs(a); }
        public static float Sqrt(float a) { return (float)Math.Sqrt(a); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Sign(float a) { return a >= 0f ? 1f : -1f; }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int RoundToInt(float a) { return (int)Math.Round(a); }
        public static float Rad2Deg { get { return 57.29578f; } }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        public static int CeilToInt(float a) { return (int)Math.Ceiling(a); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    }

    public enum KeyCode { E = 101, Space = 32, F = 102, LeftShift = 303 }

    public static class Input
    {
        public static bool GetKeyDown(KeyCode k) { return false; }
        public static bool GetMouseButtonDown(int b) { return false; }
        public static float GetAxis(string axis) { return 0f; }
    }

    public static class Screen
    {
        public static int width = 1920;
        public static int height = 1080;
        public static Rect safeArea { get { return new Rect(0, 0, width, height); } }
    }

    public static class JsonUtility
    {
        public static string ToJson(object obj, bool prettyPrint)
        {
            return StubJson.Serialize(obj, prettyPrint);
        }

        public static T FromJson<T>(string json)
        {
            return StubJson.Deserialize<T>(json);
        }
    }

    // ---- field-based JSON mirroring Unity's JsonUtility behaviour (fields only) ----
    public static class StubJson
    {
        private const System.Reflection.BindingFlags All =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        public static string Serialize(object o, bool pretty)
        {
            var sb = new System.Text.StringBuilder();
            WriteValue(sb, o, 0);
            return sb.ToString();
        }

        private static void WriteValue(System.Text.StringBuilder sb, object o, int depth)
        {
            if (o == null) { sb.Append("null"); return; }
            Type t = o.GetType();
            if (t.IsPrimitive || t == typeof(string))
            {
                if (t == typeof(string)) { sb.Append('"').Append(((string)o).Replace("\"", "\\\"")).Append('"'); }
                else sb.Append(Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant());
                return;
            }
            if (o is System.Collections.IList list)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in list) { if (!first) sb.Append(','); first = false; WriteValue(sb, item, depth + 1); }
                sb.Append(']');
                return;
            }
            sb.Append('{');
            bool f = true;
            foreach (var field in t.GetFields(All))
            {
                if (!f) sb.Append(',');
                f = false;
                sb.Append('"').Append(field.Name).Append("\":");
                WriteValue(sb, field.GetValue(o), depth + 1);
            }
            sb.Append('}');
        }

        public static T Deserialize<T>(string json)
        {
            object result = Activator.CreateInstance(typeof(T));
            Fill(result, json);
            return (T)result;
        }

        private static void Fill(object target, string json)
        {
            if (target == null) return;
            Type t = target.GetType();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(json, "\"([A-Za-z0-9_]+)\":([^,{}]+)"))
            {
                string name = m.Groups[1].Value;
                string raw = m.Groups[2].Value.Trim().Trim('"').Trim('"');
                var field = t.GetField(name, All);
                if (field == null) continue;
                Type ft = field.FieldType;
                object val = null;
                if (ft == typeof(string)) val = raw.Trim('"');
                else if (ft == typeof(int)) val = int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                else if (ft == typeof(bool)) val = raw.ToLowerInvariant() == "true";
                else if (ft == typeof(float)) val = float.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
                if (val != null) field.SetValue(target, val);
            }
        }
    }

    public class Collider : Component
    {
        public bool isTrigger;
        public bool enabled = true;
    }

    public class BoxCollider : Collider { public Vector3 size, center; }
    public class SphereCollider : Collider { public float radius; public Vector3 center; }
    public class CapsuleCollider : Collider { public float radius, height; public int direction; public Vector3 center; }
    public class MeshFilter : Component { }
    public class MeshRenderer : Renderer { }

    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public bool enabled = true;
    }

    public class Material : Object
    {
        public Color color { get; set; }
        public Color mainColor { get { return color; } }
        public void SetColor(string name, Color value) { }
        public Material(Shader shader) { }
    }
    public class Shader : Object
    {
        public static Shader Find(string name) { return null; }
    }

    public class CharacterController : Collider
    {
        public float height, radius, stepOffset, skinWidth;
        public Vector3 center;
        public bool isGrounded;
        public void Move(Vector3 motion) { }
    }

    public class Animator : Behaviour
    {
        public RuntimeAnimatorController runtimeAnimatorController;
        public bool applyRootMotion;
        public void SetFloat(int id, float v) { }
        public void SetFloat(int id, float v, float damp, float dt) { }
        public void SetBool(int id, bool v) { }
        public static int StringToHash(string s) { return s.GetHashCode(); }
    }

    public class RuntimeAnimatorController : Object { }

    public class AudioListener : Behaviour { }
    public class Camera : Behaviour { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class HideInInspector : Attribute { }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class SpaceAttribute : Attribute { }
}

namespace UnityEngine.UI
{
    using UnityEngine.Events;

    public class Graphic : UnityEngine.Behaviour
    {
        public UnityEngine.RectTransform rectTransform { get { return transform as UnityEngine.RectTransform; } }
        public UnityEngine.Color color { get; set; }
        public bool raycastTarget = true;
    }

    public class Image : Graphic { public Sprite sprite; }

    public class Text : Graphic
    {
        public UnityEngine.Font font;
        public string text = "";
        public int fontSize = 14;
        public UnityEngine.TextAnchor alignment;
        public UnityEngine.FontStyle fontStyle;
        public UnityEngine.HorizontalWrapMode horizontalOverflow;
        public UnityEngine.VerticalWrapMode verticalOverflow;
        public bool supportRichText = true;
    }



    public struct ColorBlock
    {
        public UnityEngine.Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
        public static ColorBlock defaultColorBlock { get { return new ColorBlock(); } }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public ButtonClickedEvent onClick = new ButtonClickedEvent();
        public Image image { get { return targetGraphic as Image; } set { targetGraphic = value; } }
    }

    public class Selectable : Graphic
    {
        public Graphic targetGraphic;
        public ColorBlock colors;
    }

    public class CanvasScaler : UnityEngine.Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode;
        public UnityEngine.Vector2 referenceResolution;
        public ScreenMatchMode screenMatchMode;
        public float matchWidthOrHeight;
    }

    public class GraphicRaycaster : UnityEngine.Behaviour { }

    public class Canvas : UnityEngine.Behaviour
    {
        public UnityEngine.RenderMode renderMode;
    }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();

    public class UnityEvent
    {
        public void AddListener(UnityAction call) { }
        public void RemoveListener(UnityAction call) { }
        public void Invoke() { }
    }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Behaviour
    {
        public static EventSystem current;
    }
    public class StandaloneInputModule : UnityEngine.Behaviour { }
}

namespace UnityEngine.InputSystem
{
    public class Keyboard
    {
        public static Keyboard current;
        public class EKey { public bool wasPressedThisFrame { get { return false; } } }
        public EKey eKey = new EKey();
        public EKey fKey = new EKey();
        public EKey leftShiftKey = new EKey();
    }
}

namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.Behaviour { }
}
