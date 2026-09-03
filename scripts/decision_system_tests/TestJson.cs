// Field-based JSON that mirrors Unity's JsonUtility semantics (public fields only,
// no dictionaries, no properties) so the persistence round-trip below is the same
// shape the game serializes in the editor/device build.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Crossroads.Tests
{
    public static class TestJson
    {
        private const BindingFlags All = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        public static string ToJson(object o)
        {
            var sb = new StringBuilder();
            WriteValue(sb, o);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object o)
        {
            if (o == null) { sb.Append("null"); return; }
            Type t = o.GetType();

            if (t == typeof(string))
            {
                sb.Append('"').Append(((string)o).Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                return;
            }
            if (t == typeof(bool)) { sb.Append((bool)o ? "true" : "false"); return; }
            if (t.IsPrimitive || t.IsEnum)
            {
                sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture).ToLowerInvariant());
                return;
            }
            if (o is IDictionary) throw new NotSupportedException("Dictionaries are not JsonUtility-serializable (design uses entry lists)");

            if (o is IEnumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in (IEnumerable)o)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }

            sb.Append('{');
            bool f = true;
            foreach (FieldInfo field in t.GetFields(All))
            {
                if (!f) sb.Append(',');
                f = false;
                sb.Append('"').Append(field.Name).Append("\":");
                WriteValue(sb, field.GetValue(o));
            }
            sb.Append('}');
        }

        public static T FromJson<T>(string json)
        {
            var parser = new Parser(json);
            object result = parser.ParseValue(Activator.CreateInstance(typeof(T)), typeof(T));
            return (T)result;
        }

        private class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; _i = 0; }

            public object ParseValue(object target, Type type)
            {
                SkipWs();
                if (Peek() == '{') return ParseObject(target, type);
                if (Peek() == '[') return ParseArray(type);
                return ParseScalar(type);
            }

            private object ParseObject(object target, Type type)
            {
                if (target == null) target = Activator.CreateInstance(type);
                Expect('{');
                SkipWs();
                while (Peek() != '}')
                {
                    string key = ParseString();
                    Expect(':');
                    SkipWs();
                    FieldInfo field = type.GetField(key, All);
                    object value = null;
                    if (field != null)
                    {
                        Type ft = field.FieldType;
                        object current = field.GetValue(target);
                        // lists keep a live accumulator (lists are never null in our DTOs)
                        value = ParseValue(current, ft);
                        field.SetValue(target, value);
                    }
                    else
                    {
                        SkipValue();
                    }
                    SkipWs();
                    if (Peek() == ',') { _i++; SkipWs(); }
                }
                Expect('}');
                return target;
            }

            private object ParseArray(Type listType)
            {
                Expect('[');
                Type itemType = listType.GetGenericArguments()[0];
                IList list = (IList)Activator.CreateInstance(listType);
                SkipWs();
                while (Peek() != ']')
                {
                    object item = ParseValue(null, itemType);
                    list.Add(item);
                    SkipWs();
                    if (Peek() == ',') { _i++; SkipWs(); }
                }
                Expect(']');
                return list;
            }

            private object ParseScalar(Type type)
            {
                SkipWs();
                char c = Peek();
                if (c == '"')
                {
                    string s = ParseString();
                    if (type == typeof(string)) return s;
                    throw new NotSupportedException("string into " + type.Name);
                }
                string raw = ReadUntil(new[] { ',', '}', ']' });
                raw = raw.Trim();
                if (type == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
                if (type == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
                if (type == typeof(bool)) return raw == "true";
                if (type.IsEnum) return Enum.Parse(type, raw);
                if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    Type inner = Nullable.GetUnderlyingType(type);
                    if (raw == "null") return null;
                    return ParseScalar(inner);
                }
                throw new NotSupportedException("scalar into " + type.Name + " (raw=" + raw + ")");
            }

            private void SkipValue()
            {
                SkipWs();
                if (Peek() == '{')
                {
                    int depth = 0;
                    do { if (Peek() == '{') depth++; if (Peek() == '}') depth--; _i++; } while (depth > 0 && _i < _s.Length);
                }
                else if (Peek() == '[')
                {
                    int depth = 0;
                    do { if (Peek() == '[') depth++; if (Peek() == ']') depth--; _i++; } while (depth > 0 && _i < _s.Length);
                }
                else ReadUntil(new[] { ',', '}', ']' });
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\') { c = _s[_i++]; }
                    sb.Append(c);
                }
                return sb.ToString();
            }

            private string ReadUntil(char[] stops)
            {
                var sb = new StringBuilder();
                while (_i < _s.Length && Array.IndexOf(stops, _s[_i]) < 0) sb.Append(_s[_i++]);
                return sb.ToString();
            }

            private void SkipWs() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }
            private char Peek() { return _i < _s.Length ? _s[_i] : '\0'; }
            private void Expect(char c) { if (Peek() != c) throw new FormatException("expected '" + c + "' at " + _i + " in ..." + _s.Substring(Math.Max(0, _i - 20), Math.Min(40, _s.Length - Math.Max(0, _i - 20)))); _i++; }
        }
    }
}
