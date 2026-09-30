using System;
using System.Text.RegularExpressions;

namespace SMTModBrowser.Core
{
    public static class TextUtil
    {
        /// <summary>Turns README markdown into readable plain text.</summary>
        public static string MarkdownToPlain(string markdown, int maxLength = 12000)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var s = markdown.Replace("\r\n", "\n");
            s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);
            s = Regex.Replace(s, @"!\[[^\]]*\]\([^)]*\)", "");                 // images
            s = Regex.Replace(s, @"\[([^\]]*)\]\([^)]*\)", "$1");               // links → text
            s = Regex.Replace(s, @"<[^>]+>", "");                               // html tags
            s = Regex.Replace(s, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);   // headings
            s = Regex.Replace(s, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline); // bullets
            s = Regex.Replace(s, @"^\s*([-*_]\s*){3,}$", "", RegexOptions.Multiline);  // rules
            s = Regex.Replace(s, @"(\*\*|__|`{1,3})", "");                      // bold, code
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            s = s.Trim();

            // IMGUI labels have a vertex limit, so very long READMEs are cut short
            return s.Length > maxLength ? s.Substring(0, maxLength) + "…" : s;
        }

        public static string Count(long n) =>
            n >= 1_000_000 ? $"{n / 1_000_000.0:0.#}M" :
            n >= 1_000 ? $"{n / 1_000.0:0.#}k" : n.ToString();

        public static string Ago(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalDays >= 365) return Plural((int)(span.TotalDays / 365), "year");
            if (span.TotalDays >= 30) return Plural((int)(span.TotalDays / 30), "month");
            if (span.TotalDays >= 1) return Plural((int)span.TotalDays, "day");
            if (span.TotalHours >= 1) return Plural((int)span.TotalHours, "hour");
            return "just now";
        }

        static string Plural(int n, string unit) => $"{n} {unit}{(n == 1 ? "" : "s")} ago";

        public static string Size(long bytes) =>
            bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";
    }
}
