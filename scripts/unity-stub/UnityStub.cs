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
        private static int _nextInstanceID = 46000;
        private int _instanceID;
        public int GetInstanceID() { if (_instanceID == 0) _instanceID = _nextInstanceID++; return _instanceID; }
        public string name = "";
        public static void Destroy(Object o) { StubDestroy(o); }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { StubDestroy(o); }
        private static void StubDestroy(Object o)
        {
            // mirror Unity: destroying a GameObject tears down its components (OnDestroy runs)
            var go = o as GameObject;
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Component>(false)) StubOnDestroy(c);
        }
        private static void StubOnDestroy(Component c)
        {
            var m = c.GetType().GetMethod("OnDestroy",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (m != null && m.ReturnType == typeof(void)) m.Invoke(c, null);
        }
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
        public GameObject gameObject { get; internal set; }
        public Transform transform
        {
            get
            {
                if (this is Transform) return (Transform)this;
                var g = gameObject;
                return g == null ? null : g.FindTransform();
            }
        }
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
        private readonly System.Collections.Generic.List<Component> _components = new System.Collections.Generic.List<Component>();
        public Transform transform
        {
            get
            {
                foreach (var c in _components) if (c is Transform) return (Transform)c;
                return null;
            }
        }
        public int layer;
        public static GameObject CreatePrimitive(PrimitiveType type) { return new GameObject(type.ToString()); }
        public string tag = "Untagged";
        public bool activeSelf { get; private set; }
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) : this(name)
        {
            foreach (var t in components)
            {
                var c = (Component)System.Activator.CreateInstance(t);
                c.gameObject = this;
                _components.Add(c);
                StubAwake(c);
            }
        }
        public T GetComponent<T>()
        {
            foreach (var c in _components) if (c is T) return (T)(object)c;
            return default(T);
        }
        public T GetComponentInChildren<T>() { return GetComponent<T>(); }
        public T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var c in _components) if (c is T) list.Add((T)(object)c);
            return list.ToArray();
        }
        public T GetComponentInParent<T>() { return GetComponent<T>(); }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T();
            c.gameObject = this;
            _components.Add(c);
            StubAwake(c);
            return c;
        }
        private static void StubAwake(Component c)
        {
            // mirror Unity: AddComponent runs Awake immediately (when the type defines one)
            var m = c.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (m != null && m.ReturnType == typeof(void)) m.Invoke(c, null);
        }
        public void SetActive(bool v) { activeSelf = v; }
        internal Transform FindTransform()
        {
            foreach (var c in _components) if (c is Transform) return (Transform)c;
            return null;
        }
        public static GameObject FindGameObjectWithTag(string t) { return null; }
        public static GameObject Find(string name) { return null; }
    }

    public class Transform : Component
    {
        public Vector3 position;
        public Vector3 right { get { return Vector3.right; } }
        public Quaternion rotation;
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale = Vector3.one;
        public Transform parent { get; set; }
        public int childCount;
        public void SetParent(Transform p, bool worldPositionStays) { parent = p; }
        public Transform GetChild(int index) { return null; }
        public void SetPositionAndRotation(Vector3 pos, Quaternion rot) { position = pos; rotation = rot; }
        public void Rotate(float x, float y, float z) { }
        public Vector3 forward { get { return Vector3.forward; } }
    }

    public class RectTransform : Transform
    {
        public void SetAsLastSibling() { }
        public void SetAsFirstSibling() { }
        public Vector2 anchorMin, anchorMax, pivot, offsetMin, offsetMax, sizeDelta, anchoredPosition;
        public Rect rect { get { return new Rect(0f, 0f, 460f, 520f); } }
    }

    public static class RectTransformUtility
    {
        public static Vector2 WorldToScreenPoint(Camera cam, Vector3 worldPoint)
        {
            return new Vector2(worldPoint.x, worldPoint.y);
        }
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
        public static Vector2 operator *(Vector2 a, float d) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 operator *(float d, Vector2 a) { return new Vector2(a.x * d, a.y * d); }
        public static Vector2 ClampMagnitude(Vector2 v, float maxLength)
        {
            float m = v.magnitude;
            return m > maxLength && m > 0f ? v / m * maxLength : v;
        }
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
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
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
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
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
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
    }

    public struct Rect
    {
        public Vector2 position, size;
        public float width { get { return size.x; } }
        public float height { get { return size.y; } }
        public float x { get { return position.x; } }
        public float y { get { return position.y; } }
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

    public class Sprite : Object
    {
        public Rect textureRect;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, uint meshType)
        {
            var s = new Sprite();
            s.textureRect = rect;
            return s;
        }
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            return Create(texture, rect, pivot, pixelsPerUnit, 0, 0);
        }
    }

    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1 }

    public class Texture2D : Object
    {
        public int width, height;
        public FilterMode filterMode = FilterMode.Bilinear;
        public TextureWrapMode wrapMode = TextureWrapMode.Repeat;
        private UnityEngine.Color[] _pixels;
        public Texture2D(int w, int h) { width = w; height = h; _pixels = new UnityEngine.Color[w * h]; }
        public void SetPixel(int x, int y, UnityEngine.Color c) { if (x >= 0 && y >= 0 && x < width && y < height) _pixels[y * width + x] = c; }
        public void SetPixels(UnityEngine.Color[] colors) { if (colors != null && colors.Length == _pixels.Length) _pixels = (UnityEngine.Color[])colors.Clone(); }
        public UnityEngine.Color[] GetPixels() { return _pixels; }
        public void Apply() { }
    }

    public static class Debug
    {
        public static void Log(object o) { Console.WriteLine("[LOG] " + o); }
        public static void LogWarning(object o) { Console.WriteLine("[WARN] " + o); }
        public static void LogError(object o) { Console.WriteLine("[ERR] " + o); }
    }

    public static class Application
    {
        public static void Quit() { }
        public static string persistentDataPath = ".";
        public static int targetFrameRate = -1;
    }

    public static class Time
    {
        public static float time { get { return 0f; } }
        public static float deltaTime { get { return 0.016f; } }
        public static float unscaledTime { get { return 0f; } }
        public static float unscaledDeltaTime { get { return 0.016f; } }
        public static float timeScale = 1f;
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = 0.017453292519943295f;
        public static float Sin(float a) { return (float)Math.Sin(a); }
        public static float Cos(float a) { return (float)Math.Cos(a); }
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
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
        public static float Repeat(float t, float length) { return Clamp(t - (float)Math.Floor(t / length) * length, 0f, length); }
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Math.Abs(t - length); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static float Exp(float a) { return (float)Math.Exp(a); }
        public static int FloorToInt(float a) { return (int)Math.Floor(a); }
    }

    public enum KeyCode { E = 101, Space = 32, F = 102, F3 = 284, LeftShift = 303 }

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

    public class MaterialPropertyBlock
    {
        private readonly System.Collections.Generic.Dictionary<string, object> _vals = new System.Collections.Generic.Dictionary<string, object>();
        public void SetColor(string name, Color c) { _vals[name] = c; }
        public void SetFloat(string name, float f) { _vals[name] = f; }
        public void SetVector(string name, Vector3 v) { _vals[name] = v; }
        public Color GetColor(string name) { return _vals.ContainsKey(name) ? (Color)_vals[name] : Color.white; }
    }

    public class Renderer : Component
    {
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public bool enabled = true;
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
        public bool receiveShadows = true;
        public void SetPropertyBlock(MaterialPropertyBlock block) { }
    }

    public class Material : Object
    {
        public Color color { get; set; }
        public Color mainColor { get { return color; } }
        public bool enableInstancing;
        public void SetColor(string name, Color value) { }
        public void SetFloat(string name, float value) { }
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
        public void SetTrigger(int id) { }
        public void ResetTrigger(int id) { }
        public static int StringToHash(string s) { return s.GetHashCode(); }
    }

    public class RuntimeAnimatorController : Object { }

    public class CanvasGroup : Behaviour
    {
        public float alpha = 1f;
        public bool interactable = true;
        public bool blocksRaycasts = true;
    }
    public class AudioListener : Behaviour { public static float volume = 1f; }
    public class AudioClip : Object { public float length = 1f; }
    public class AudioSource : Behaviour
    {
        public AudioClip clip;
        public bool playOnAwake = true, loop;
        public float volume = 1f, pitch = 1f, spatialBlend;
        public bool isPlaying { get; private set; }
        public void Play() { isPlaying = clip != null; }
        public void Stop() { isPlaying = false; }
        public void PlayOneShot(AudioClip c, float v) { }
    }
    public static class Random
    {
        private static readonly System.Random Rng = new System.Random(7);
        public static float value { get { return (float)Rng.NextDouble(); } }
        public static float Range(float a, float b) { return a + (b - a) * value; }
        public static int Range(int a, int b) { return Rng.Next(a, b); }
    }
    public static class QualitySettings
    {
        private static int _level = 1;
        public static string[] names = { "Low", "Balanced", "High" };
        public static int GetQualityLevel() { return _level; }
        public static void SetQualityLevel(int index, bool applyExpensiveChanges) { _level = index; }
        public static int vSyncCount;
    }
    public class Camera : Behaviour
    {
        public static Camera main;
        public float fieldOfView = 60f;
        public Vector3 WorldToScreenPoint(Vector3 worldPoint) { return new Vector3(worldPoint.x, worldPoint.y, worldPoint.z); }
        public bool PixelRectContains(Vector3 screenPoint) { return true; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    public class SerializeField : Attribute { }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public class HideInInspector : Attribute { }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class SpaceAttribute : Attribute { }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public class VolumeProfile : ScriptableObject { }
    public class Volume : MonoBehaviour { public VolumeProfile sharedProfile; public bool isGlobal = true; }
}

namespace UnityEngine
{
    public static class Physics
    {
        // headless: no geometry, so probes always report "all clear"
        public static bool SphereCast(Ray origin, float radius, out RaycastHit hitInfo, float maxDistance)
        {
            hitInfo = default(RaycastHit);
            return false;
        }
    }
    public struct RaycastHit { public Vector3 point; public Vector3 normal; public float distance; public Collider collider; }
    public struct Ray { public Vector3 origin; public Vector3 direction; public Ray(Vector3 o, Vector3 d) { origin = o; direction = d; } }
}

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : UnityEngine.MonoBehaviour { }
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

    public class Image : Graphic
    {
        public Sprite sprite;
        // real uGUI filled-image API (radial cooldown sweeps, bar fills)
        public Type type = Type.Simple;
        public FillMethod fillMethod = FillMethod.Radial360;
        public int fillOrigin = 0;
        public bool fillClockwise = true;
        public float fillAmount = 1f;
        public bool preserveAspect = false;

        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
    }

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

    // Real uGUI hierarchy: Selectable : UIBehaviour : MonoBehaviour. Selectable is NOT a Graphic,
    // so Button has no rectTransform / color / raycastTarget (Unity 6 compile error CS1061 that the
    // previous stub hid). Mirror reality here so the local compile fails where the editor would.
    public class Selectable : UnityEngine.EventSystems.UIBehaviour
    {
        public Graphic targetGraphic;
        public ColorBlock colors;
        public bool interactable = true;
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

    // world expansion phase: environment application (locations) uses the real Unity APIs
    // below; the headless stub mirrors just the surface the prototype touches.
    public class Light : Behaviour
    {
        public Color color = new Color(1f, 1f, 1f, 1f);
        public float intensity = 1f;
    }

    public static class RenderSettings
    {
        public static Color ambientLight = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static bool fog;
        public static Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static float fogDensity;
    }

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

    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }

    public class PointerEventData
    {
        public Vector2 position;
        public Vector2 pressPosition;
        public Vector2 delta;
        public GameObject pressedObject;
        public bool dragging;
        public int pointerId;
    }
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
