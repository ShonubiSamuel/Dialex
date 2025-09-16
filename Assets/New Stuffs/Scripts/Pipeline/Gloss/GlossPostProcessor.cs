using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YourApp.Signs.Pipeline.Gloss
{
    /// <summary>
    /// Utilities to parse LLM output, normalize to stable keys, and clean the list.
    /// </summary>
    public static class GlossPostProcessor
    {
        // Matches JSON array of strings quickly (no heavy JSON lib).
        private static readonly Regex RxJsonItem =
            new Regex("\"([^\"]+)\"", RegexOptions.Compiled);

        // Matches bullets like: "- ask_out", "1) anyone", "• arrive"
        private static readonly Regex RxBullet =
            new Regex(@"^\s*(?:[-•*]|\d+[\.\)]\s*)?\s*(.+?)\s*$",
                      RegexOptions.Compiled | RegexOptions.Multiline);

        // Looks like a comma-separated inline list
        private static readonly Regex RxCommaSplit =
            new Regex(@",\s*", RegexOptions.Compiled);

        // Decimal like 7 . 2 or 7 .2 or 7. 2 → join to 7.2
        private static readonly Regex RxDecimalSpaces =
            new Regex(@"^(\d+)\s*[\.,]\s*(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Parse a block of text (model reply) into a raw list of gloss strings.
        /// Accepts JSON array, newline bullets, or comma-separated.
        /// </summary>
        public static List<string> ParseGlossBlock(string text)
        {
            var outList = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return outList;

            // 1) Try JSON array of strings
            var jsonHits = RxJsonItem.Matches(text);
            if (jsonHits.Count > 0)
            {
                foreach (Match m in jsonHits) AddNonEmpty(outList, m.Groups[1].Value);
                return outList;
            }

            // 2) Try line bullets
            var lineHits = RxBullet.Matches(text);
            if (lineHits.Count > 0)
            {
                foreach (Match m in lineHits) AddNonEmpty(outList, m.Groups[1].Value);
                return SplitCommaFallback(outList); // handle inline comma in bullet
            }

            // 3) Fallback: comma-separated
            foreach (var s in RxCommaSplit.Split(text))
                AddNonEmpty(outList, s);

            return outList;
        }

        /// <summary>
        /// Full cleaning pipeline: trim, normalize, dedupe consecutive, merge decimals.
        /// </summary>
        public static List<string> Process(List<string> raw)
        {
            var list = new List<string>();
            if (raw == null) return list;

            // 1) Trim + fix obvious decimal spacing before normalization
            var pre = new List<string>(raw.Count);
            foreach (var r in raw)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                var s = r.Trim();

                // Join “7 . 2” → “7.2”
                var m = RxDecimalSpaces.Match(s);
                if (m.Success) s = $"{m.Groups[1].Value}.{m.Groups[2].Value}";

                pre.Add(s);
            }

            // 2) Normalize (lowercase, underscores, strip parens, etc.)
            foreach (var s in pre)
            {
                var k = GlossNormalizer.Normalize(s);
                if (!string.IsNullOrEmpty(k)) list.Add(k);
            }

            // 3) Merge pure numeric tokens that were split oddly (rare fallback)
            list = MergeSplitNumerics(list);

            // 4) Deduplicate consecutive duplicates
            for (int i = list.Count - 1; i > 0; i--)
                if (list[i] == list[i - 1]) list.RemoveAt(i);

            return list;
        }

        private static void AddNonEmpty(List<string> acc, string s)
        {
            if (!string.IsNullOrWhiteSpace(s)) acc.Add(s.Trim());
        }

        private static List<string> SplitCommaFallback(List<string> fromBullets)
        {
            var outList = new List<string>();
            foreach (var line in fromBullets)
            {
                if (line.Contains(","))
                {
                    var parts = RxCommaSplit.Split(line);
                    foreach (var p in parts) AddNonEmpty(outList, p);
                }
                else AddNonEmpty(outList, line);
            }
            return outList;
        }

        // If model emitted ["7",".","2"] after normalization → join to "7.2"
        private static List<string> MergeSplitNumerics(List<string> src)
        {
            if (src.Count < 3) return src;
            var dst = new List<string>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                if (i + 2 < src.Count &&
                    IsDigits(src[i]) &&
                    (src[i + 1] == "." || src[i + 1] == "point") &&
                    IsDigits(src[i + 2]))
                {
                    dst.Add($"{src[i]}.{src[i + 2]}");
                    i += 2;
                }
                else dst.Add(src[i]);
            }
            return dst;
        }

        private static bool IsDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] < '0' || s[i] > '9') return false;
            return true;
        }
    }
}
