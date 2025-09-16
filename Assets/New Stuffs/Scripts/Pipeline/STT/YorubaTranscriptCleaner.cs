using System.Text.RegularExpressions;

public static class YorubaTranscriptCleaner
{
    // Remove noise/filler tags and unwrap <foreignS>...</foreignE>. Also squashes spammy repeats.
    public static string CleanForTranslation(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // 1) Remove known ASR markup
        s = Regex.Replace(s, @"<\s*CNOISE\s*/>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*FILL\s*/>",   " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*NOISE\s*/>",  " ", RegexOptions.IgnoreCase);

        // 2) Unwrap foreign-language spans: <foreignS>phone<foreignE> -> phone
        s = Regex.Replace(s, @"<\s*foreignS\s*>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*foreignE\s*>", "", RegexOptions.IgnoreCase);

        // 3) Drop any other angle-bracket leftovers (paranoid)
        s = Regex.Replace(s, @"<[^>]+>", " ");

        // 4) Collapse extreme repetition: three-or-more identical words -> single
        //    e.g., "phone phone phone" -> "phone"
        s = Regex.Replace(s, @"\b(\w+)(?:\s+\1){2,}\b", "$1", RegexOptions.IgnoreCase);

        // 5) Normalize whitespace & commas
        s = Regex.Replace(s, @"\s+", " ").Trim();
        s = Regex.Replace(s, @"\s+,", ",");
        s = Regex.Replace(s, @",\s*,+", ", ");

        return s;
    }
}