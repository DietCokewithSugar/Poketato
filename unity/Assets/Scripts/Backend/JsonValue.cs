using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Poketato.Backend
{
    /// <summary>Small JSON tree. WebGL builds do not reference Newtonsoft.</summary>
    public sealed class JsonValue
    {
        public enum Kind { Null, Bool, Number, String, Array, Object }

        public Kind Type;
        public bool Bool;
        public double Number;
        public string String;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public static readonly JsonValue Null = new JsonValue { Type = Kind.Null };

        public bool IsNull => Type == Kind.Null;

        public JsonValue this[string key]
        {
            get
            {
                JsonValue value;
                if (Type == Kind.Object && Object != null && Object.TryGetValue(key, out value)) return value;
                return Null;
            }
        }

        public JsonValue this[int index]
        {
            get
            {
                if (Type == Kind.Array && Array != null && index >= 0 && index < Array.Count) return Array[index];
                return Null;
            }
        }

        public string AsString()
        {
            if (Type == Kind.String) return String ?? "";
            if (Type == Kind.Number) return AsLong().ToString(CultureInfo.InvariantCulture);
            if (Type == Kind.Bool) return Bool ? "true" : "false";
            return null;
        }

        public long AsLong()
        {
            if (Type == Kind.Number) return (long)Number;
            long parsed;
            if (Type == Kind.String && long.TryParse(String, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return parsed;
            return 0;
        }

        public bool AsBool()
        {
            if (Type == Kind.Bool) return Bool;
            return Type == Kind.String && String == "true";
        }

        public static JsonValue Obj()
        {
            return new JsonValue { Type = Kind.Object, Object = new Dictionary<string, JsonValue>() };
        }

        public static JsonValue Arr()
        {
            return new JsonValue { Type = Kind.Array, Array = new List<JsonValue>() };
        }

        public JsonValue Set(string key, string value)
        {
            Object[key] = value == null ? Null : new JsonValue { Type = Kind.String, String = value };
            return this;
        }

        public JsonValue Set(string key, long value)
        {
            Object[key] = new JsonValue { Type = Kind.Number, Number = value };
            return this;
        }

        public JsonValue Set(string key, bool value)
        {
            Object[key] = new JsonValue { Type = Kind.Bool, Bool = value };
            return this;
        }

        public JsonValue Set(string key, JsonValue value)
        {
            Object[key] = value ?? Null;
            return this;
        }

        public static JsonValue Parse(string text)
        {
            if (text == null) throw new FormatException("json is null");
            var parser = new Parser(text);
            var value = parser.Read();
            parser.Skip();
            if (!parser.Done) throw new FormatException("trailing json");
            return value;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            Write(sb);
            return sb.ToString();
        }

        void Write(StringBuilder sb)
        {
            switch (Type)
            {
                case Kind.Null:
                    sb.Append("null");
                    break;
                case Kind.Bool:
                    sb.Append(Bool ? "true" : "false");
                    break;
                case Kind.Number:
                    if (Number == Math.Truncate(Number) && Number >= long.MinValue && Number <= long.MaxValue)
                        sb.Append(((long)Number).ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append(Number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Kind.String:
                    WriteString(sb, String ?? "");
                    break;
                case Kind.Array:
                    sb.Append('[');
                    if (Array != null)
                    {
                        for (int i = 0; i < Array.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            (Array[i] ?? Null).Write(sb);
                        }
                    }
                    sb.Append(']');
                    break;
                case Kind.Object:
                    sb.Append('{');
                    if (Object != null)
                    {
                        bool first = true;
                        foreach (var pair in Object)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            WriteString(sb, pair.Key);
                            sb.Append(':');
                            (pair.Value ?? Null).Write(sb);
                        }
                    }
                    sb.Append('}');
                    break;
            }
        }

        static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string s;
            int i;
            int depth;
            public bool Done => i >= s.Length;

            public Parser(string text) { s = text; }

            public void Skip()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public JsonValue Read()
            {
                if (++depth > 32) throw new FormatException("json too deep");
                Skip();
                if (i >= s.Length) throw new FormatException("unexpected end");
                char c = s[i];
                JsonValue value;
                if (c == '{') value = ReadObject();
                else if (c == '[') value = ReadArray();
                else if (c == '"') value = new JsonValue { Type = Kind.String, String = ReadString() };
                else if (c == 't') value = ReadLiteral("true", new JsonValue { Type = Kind.Bool, Bool = true });
                else if (c == 'f') value = ReadLiteral("false", new JsonValue { Type = Kind.Bool, Bool = false });
                else if (c == 'n') value = ReadLiteral("null", Null);
                else if (c == '-' || (c >= '0' && c <= '9')) value = ReadNumber();
                else throw new FormatException("bad json at " + i);
                depth--;
                return value;
            }

            JsonValue ReadObject()
            {
                i++;
                var obj = Obj();
                Skip();
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                while (true)
                {
                    Skip();
                    if (i >= s.Length || s[i] != '"') throw new FormatException("expected key");
                    string key = ReadString();
                    Skip();
                    if (i >= s.Length || s[i] != ':') throw new FormatException("expected colon");
                    i++;
                    obj.Object[key] = Read();
                    Skip();
                    if (i >= s.Length) throw new FormatException("unclosed object");
                    if (s[i] == '}') { i++; return obj; }
                    if (s[i] != ',') throw new FormatException("expected comma");
                    i++;
                }
            }

            JsonValue ReadArray()
            {
                i++;
                var arr = Arr();
                Skip();
                if (i < s.Length && s[i] == ']') { i++; return arr; }
                while (true)
                {
                    arr.Array.Add(Read());
                    Skip();
                    if (i >= s.Length) throw new FormatException("unclosed array");
                    if (s[i] == ']') { i++; return arr; }
                    if (s[i] != ',') throw new FormatException("expected comma");
                    i++;
                }
            }

            string ReadString()
            {
                i++;
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (i >= s.Length) throw new FormatException("bad escape");
                        char e = s[i++];
                        switch (e)
                        {
                            case '"':
                            case '\\':
                            case '/': sb.Append(e); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 > s.Length) throw new FormatException("bad unicode");
                                int code = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                sb.Append((char)code);
                                i += 4;
                                break;
                            default: throw new FormatException("bad escape");
                        }
                    }
                    else sb.Append(c);
                }
                throw new FormatException("unclosed string");
            }

            JsonValue ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new FormatException("bad number");
                if (s[i] == '0') i++;
                else while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                if (i < s.Length && s[i] == '.')
                {
                    i++;
                    if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new FormatException("bad number");
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    if (i >= s.Length || s[i] < '0' || s[i] > '9') throw new FormatException("bad number");
                    while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
                }
                double number = double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
                return new JsonValue { Type = Kind.Number, Number = number };
            }

            JsonValue ReadLiteral(string word, JsonValue value)
            {
                if (i + word.Length > s.Length || s.Substring(i, word.Length) != word) throw new FormatException("bad literal");
                i += word.Length;
                return value;
            }
        }
    }
}
