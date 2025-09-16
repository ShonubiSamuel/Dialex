using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// Utilities to canonicalize numeric-like keys and generate aliases.
/// Canonical policy:
///   - integers: "0","1","10"
///   - decimals: "7_2" (dot becomes underscore)
/// Aliases accepted: dotted ("7.2"), collapsed ("72"/"01"), and worded ("seven_point_two").
internal static class NumericKeyUtil
{
    private static readonly Dictionary<string,string> WordToDigit = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"]="0", ["one"]="1", ["two"]="2", ["three"]="3", ["four"]="4",
        ["five"]="5", ["six"]="6", ["seven"]="7", ["eight"]="8", ["nine"]="9",
        ["ten"]="10", ["eleven"]="11", ["twelve"]="12", ["thirteen"]="13",
        ["fourteen"]="14", ["fifteen"]="15", ["sixteen"]="16",
        ["seventeen"]="17", ["eighteen"]="18", ["nineteen"]="19"
    };

    private static readonly string[] DigitToWord =
    {
        "zero","one","two","three","four","five","six","seven","eight","nine",
        "ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen",
        "seventeen","eighteen","nineteen"
    };

    private static readonly Regex DecDot = new(@"(?<=\d)\.(?=\d)", RegexOptions.Compiled);
    private static readonly Regex KeepNumUnd = new(@"[^a-z0-9_]", RegexOptions.Compiled);

    public static string Canonicalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var s = key.Trim().ToLowerInvariant();

        // normalize joins
        s = s.Replace("point", "_");

        // 7.2 -> 7_2
        s = DecDot.Replace(s, "_");

        // word numbers to digits per token
        var parts = s.Split('_');
        for (int i=0;i<parts.Length;i++)
        {
            if (WordToDigit.TryGetValue(parts[i], out var d))
                parts[i] = d;
        }
        s = string.Join("_", parts);

        // collapse multiple underscores, drop junk, trim
        s = Regex.Replace(s, "_{2,}", "_");
        s = KeepNumUnd.Replace(s, "");
        s = s.Trim('_');

        return s;
    }

    public static IEnumerable<string> Variants(string canonical)
    {
        if (string.IsNullOrEmpty(canonical)) yield break;
        yield return canonical; // primary

        if (canonical.Contains("_"))
        {
            yield return canonical.Replace("_", "."); // dotted
            yield return canonical.Replace("_", "");  // collapsed
            // worded form (only simple a_b or 0..19 supported)
            var w = ToWorded(canonical);
            if (!string.IsNullOrEmpty(w)) yield return w;
        }
        else
        {
            // integer worded if small
            if (int.TryParse(canonical, out var n) && n >= 0 && n < DigitToWord.Length)
                yield return DigitToWord[n];
        }
    }

    private static string ToWorded(string canon)
    {
        var parts = canon.Split('_');
        if (parts.Length == 1)
        {
            if (int.TryParse(parts[0], out var n) && n >= 0 && n < DigitToWord.Length)
                return DigitToWord[n];
            return null;
        }
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out var a) && a >= 0 && a < DigitToWord.Length &&
                int.TryParse(parts[1], out var b) && b >= 0 && b < DigitToWord.Length)
                return $"{DigitToWord[a]}_point_{DigitToWord[b]}";
        }
        return null;
    }
}
