using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Converts raw text (e.g., "Ask Out", "  Ásk-out!!  ") to a normalized sign key: "ask_out".
/// Mirrors the normalization used when generating keys from filenames.
/// </summary>
public static class GlossNormalizer
{
    /// <summary>
    /// Normalize any user/gloss text to a stable key (lowercase, underscores, [a-z0-9_]).
    /// Examples:
    ///   "Ask Out"           -> "ask_out"
    ///   "Antigua & Barb..." -> "antigua_and_barbados"
    ///   "Ascend (Variant)"  -> "ascend"
    /// </summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        string s = raw.Trim();

        // Replace common joiners with words to keep parity with filename->key rules.
        s = s.Replace("&", " and ").Replace("+", " and ");

        // Remove parentheticals e.g. "(Alt)", "(Variant)", "(Donkey)".
        s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");

        // Collapse whitespace.
        s = Regex.Replace(s, @"\s+", " ").Trim();

        // To lowercase.
        s = s.ToLowerInvariant();

        // Remove diacritics (á -> a).
        s = RemoveDiacritics(s);

        // Replace spaces with underscores.
        s = s.Replace(' ', '_');

        // Keep only [a-z0-9_].
        s = Regex.Replace(s, @"[^a-z0-9_]", "");

        // Remove trailing underscores (if any).
        s = s.Trim('_');

        return s;
    }

    private static string RemoveDiacritics(string s)
    {
        var norm = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(capacity: s.Length);

        foreach (var ch in norm)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}