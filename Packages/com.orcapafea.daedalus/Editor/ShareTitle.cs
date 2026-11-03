using System;
using System.Text;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// The game title as the class gallery's card will show it, and the default zip filename. <see cref="Clean"/> is a port of
    /// the gallery's <c>clean_label</c> (Deploy/gallery/src/text.rs); the two must stay identical, and Tools/share_check.py
    /// checks them against each other on a real zip.
    /// </summary>
    internal static class ShareTitle
    {
        /// <summary>The gallery's <c>title_max_chars</c> (Deploy/gallery/src/config.rs), in Unicode characters.</summary>
        internal const int MaxChars = 40;

        internal static string Clean(string raw)
        {
            return Clean(raw, MaxChars);
        }

        /// <summary>Drops control and bidi characters, collapses whitespace runs to one space, keeps the first
        /// <paramref name="maxChars"/> characters (a surrogate pair is one) and trims; null when nothing is left.</summary>
        internal static string Clean(string raw, int maxChars)
        {
            if (raw == null)
                return null;
            var output = new StringBuilder(raw.Length);
            int chars = 0;
            bool pendingSpace = false;
            for (int i = 0; i < raw.Length && chars < maxChars; i++)
            {
                int scalar;
                if (char.IsHighSurrogate(raw[i]) && i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1]))
                {
                    scalar = char.ConvertToUtf32(raw[i], raw[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(raw[i]))
                {
                    continue; // a lone surrogate: a Rust string can't hold one
                }
                else
                {
                    scalar = raw[i];
                }

                bool whitespace = IsWhitespace(scalar);
                if (IsControl(scalar) && !whitespace || IsBidiControl(scalar))
                    continue;
                if (whitespace)
                {
                    pendingSpace = output.Length > 0;
                    continue;
                }
                if (pendingSpace)
                {
                    output.Append(' ');
                    chars++;
                    pendingSpace = false;
                    if (chars == maxChars)
                        break;
                }
                output.Append(char.ConvertFromUtf32(scalar));
                chars++;
            }
            string capped = output.ToString().TrimEnd(' ');
            return capped.Length > 0 ? capped : null;
        }

        /// <summary>The default zip filename: the cleaned title with characters no file system allows replaced.</summary>
        internal static string FileName(string cleanTitle)
        {
            var name = new StringBuilder(cleanTitle ?? "");
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c < 0x20 || c == 0x7F || "\\/:*?\"<>|".IndexOf(c) >= 0)
                    name[i] = '-';
            }
            string result = name.ToString().TrimEnd('.', ' ');
            if (result.Length == 0)
                result = "my-game";
            else if (IsReservedOnWindows(result))
                result += "-game";
            return result + ".zip";
        }

        // Rust's char::is_whitespace (the Unicode White_Space property), spelled out so Mono's tables can't drift from it.
        static bool IsWhitespace(int c)
        {
            return c >= 0x09 && c <= 0x0D || c == 0x20 || c == 0x85 || c == 0xA0 || c == 0x1680
                || c >= 0x2000 && c <= 0x200A || c == 0x2028 || c == 0x2029 || c == 0x202F || c == 0x205F || c == 0x3000;
        }

        // Rust's char::is_control (general category Cc).
        static bool IsControl(int c)
        {
            return c <= 0x1F || c >= 0x7F && c <= 0x9F;
        }

        static bool IsBidiControl(int c)
        {
            return c >= 0x202A && c <= 0x202E || c >= 0x2066 && c <= 0x2069 || c == 0x200E || c == 0x200F || c == 0x061C;
        }

        static bool IsReservedOnWindows(string name)
        {
            string upper = name.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
                return true;
            return upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal)) && upper[3] >= '1' && upper[3] <= '9';
        }
    }
}
