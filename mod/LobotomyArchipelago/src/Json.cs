using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LobotomyArchipelago
{
    /// <summary>
    /// Minimal JSON reader/writer. Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers long or double, plus string, bool and null.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var parser = new Parser(text);
            parser.SkipWhitespace();
            object value = parser.ReadValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw new FormatException("Unexpected data after JSON value at " + parser.Position);
            }
            return value;
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }

        private static void Write(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
            }
            else if (value is string)
            {
                WriteString(builder, (string)value);
            }
            else if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
            }
            else if (value is IDictionary)
            {
                var dict = (IDictionary)value;
                builder.Append('{');
                bool first = true;
                foreach (DictionaryEntry entry in dict)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    builder.Append(':');
                    Write(builder, entry.Value);
                }
                builder.Append('}');
            }
            else if (value is IEnumerable)
            {
                builder.Append('[');
                bool first = true;
                foreach (object element in (IEnumerable)value)
                {
                    if (!first) builder.Append(',');
                    first = false;
                    Write(builder, element);
                }
                builder.Append(']');
            }
            else if (value is float || value is double || value is decimal)
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is IConvertible)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else
            {
                WriteString(builder, value.ToString());
            }
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _pos;

            public Parser(string text)
            {
                _text = text ?? string.Empty;
            }

            public bool AtEnd { get { return _pos >= _text.Length; } }
            public int Position { get { return _pos; } }

            public void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos])) _pos++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw new FormatException("Unexpected end of JSON");
                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0)
                {
                    throw new FormatException("Expected '" + word + "' at " + _pos);
                }
                _pos += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                _pos++; // {
                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] == '}')
                {
                    _pos++;
                    return result;
                }
                while (true)
                {
                    SkipWhitespace();
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _text[_pos] != ':') throw new FormatException("Expected ':' at " + _pos);
                    _pos++;
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw new FormatException("Unterminated object");
                    char c = _text[_pos++];
                    if (c == '}') return result;
                    if (c != ',') throw new FormatException("Expected ',' or '}' at " + (_pos - 1));
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (_pos < _text.Length && _text[_pos] == ']')
                {
                    _pos++;
                    return result;
                }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw new FormatException("Unterminated array");
                    char c = _text[_pos++];
                    if (c == ']') return result;
                    if (c != ',') throw new FormatException("Expected ',' or ']' at " + (_pos - 1));
                }
            }

            private string ReadString()
            {
                if (AtEnd || _text[_pos] != '"') throw new FormatException("Expected string at " + _pos);
                _pos++;
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw new FormatException("Unterminated string");
                    char c = _text[_pos++];
                    if (c == '"') return builder.ToString();
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }
                    if (AtEnd) throw new FormatException("Unterminated escape");
                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length) throw new FormatException("Bad unicode escape");
                            builder.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture));
                            _pos += 4;
                            break;
                        default:
                            throw new FormatException("Bad escape '\\" + e + "'");
                    }
                }
            }

            private object ReadNumber()
            {
                int start = _pos;
                while (_pos < _text.Length && "+-0123456789.eE".IndexOf(_text[_pos]) >= 0) _pos++;
                string token = _text.Substring(start, _pos - start);
                if (token.Length == 0) throw new FormatException("Unexpected character at " + start);
                long integer;
                if (token.IndexOfAny(new[] { '.', 'e', 'E' }) < 0 &&
                    long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                {
                    return integer;
                }
                return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Convenience accessors for parsed JSON.</summary>
    public static class JsonExt
    {
        public static Dictionary<string, object> Obj(this Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        public static List<object> Arr(this Dictionary<string, object> dict, string key)
        {
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value as List<object> : null;
        }

        public static string Str(this Dictionary<string, object> dict, string key, string fallback = "")
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null) return fallback;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static long Long(this Dictionary<string, object> dict, string key, long fallback = 0)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null) return fallback;
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static int Int(this Dictionary<string, object> dict, string key, int fallback = 0)
        {
            return (int)dict.Long(key, fallback);
        }

        public static bool Bool(this Dictionary<string, object> dict, string key, bool fallback = false)
        {
            object value;
            if (dict == null || !dict.TryGetValue(key, out value) || value == null) return fallback;
            if (value is bool) return (bool)value;
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
            }
            catch
            {
                return fallback;
            }
        }

        public static long ToLong(object value, long fallback = 0)
        {
            if (value == null) return fallback;
            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
