using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SMTModBrowser.Core
{
    /// <summary>
    /// Minimal JSON reader. Objects become Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;,
    /// numbers double. Unity's JsonUtility can't read top-level arrays, so we bring our own.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var i = 0;
            var value = ParseValue(text, ref i);
            SkipWhitespace(text, ref i);
            if (i != text.Length) throw new FormatException($"Unexpected character at {i}");
            return value;
        }

        // Typed accessors for reading parsed objects
        public static string Str(this Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) ? v as string ?? v?.ToString() : null;

        public static long Long(this Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is double d ? (long)d : 0;

        public static bool Bool(this Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is bool b && b;

        public static List<object> List(this Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        public static DateTime Date(this Dictionary<string, object> o, string key) =>
            DateTime.TryParse(o.Str(key), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
                ? d : DateTime.MinValue;

        static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ParseNumber(s, ref i);
            }
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                var key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (s[i++] != ':') throw new FormatException($"Expected ':' at {i - 1}");
                result[key] = ParseValue(s, ref i);
                SkipWhitespace(s, ref i);
                var c = s[i++];
                if (c == '}') return result;
                if (c != ',') throw new FormatException($"Expected ',' or '}}' at {i - 1}");
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                var c = s[i++];
                if (c == ']') return result;
                if (c != ',') throw new FormatException($"Expected ',' or ']' at {i - 1}");
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException($"Expected string at {i}");
            i++;
            var sb = new StringBuilder();
            while (true)
            {
                var c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                var e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: throw new FormatException($"Bad escape '\\{e}' at {i - 1}");
                }
            }
        }

        static double ParseNumber(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException($"Unexpected character '{s[i]}' at {i}");
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw new FormatException($"Expected '{word}' at {i}");
            i += word.Length;
        }

        static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
