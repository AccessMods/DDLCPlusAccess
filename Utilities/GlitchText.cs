using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Detects the random "glitch text" the game generates (RenpyStandardProxyLib.glitchtext),
    /// which a screen reader would otherwise spell out as a stream of odd symbols.
    ///
    /// The generator draws from Latin-1 symbols and Latin Extended-A letters (U+0100-U+017F).
    /// None of the game's languages use Extended-A in normal text (except French 'œ'), so a word
    /// containing several of them - or made mostly of them - is treated as glitched.
    /// </summary>
    public static class GlitchText
    {
        // Note: '·' is deliberately absent - Chinese uses it inside foreign names.
        private const string RareSymbols = "¢£¤¥¦§¨©¬®¯±²³´¶¸¹¼½¾×÷ÞþÐð";

        private static readonly Regex s_Tokens = new Regex(@"\S+", RegexOptions.Compiled);

        private static bool IsRare(char c)
        {
            if (c >= 'Ā' && c <= 'ſ')
                return c != 'œ' && c != 'Œ';
            return RareSymbols.IndexOf(c) >= 0;
        }

        public static bool IsGlitchedWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            int rare = 0;
            int nonAscii = 0;
            foreach (char c in word)
            {
                if (IsRare(c))
                    rare++;
                if (c > 0x7F)
                    nonAscii++;
            }
            return rare >= 2 || (rare >= 1 && nonAscii * 2 >= word.Length);
        }

        /// <summary>True if any word of the text looks like generated glitch text.</summary>
        public static bool ContainsGlitch(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (Match match in s_Tokens.Matches(text))
            {
                if (IsGlitchedWord(match.Value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Replaces every run of glitched words with <paramref name="marker"/>, e.g.
        /// "But I really appreciate you understanŖ¼ŕ ĳŦ..." -> "But I really appreciate you understan (glitched text)".
        /// Very short leftovers between or next to glitched words (single symbols) are swallowed too.
        /// </summary>
        public static string Clean(string text, string marker)
        {
            if (!ContainsGlitch(text))
                return text;

            var tokens = new List<string>();
            foreach (Match match in s_Tokens.Matches(text))
                tokens.Add(match.Value);

            var glitched = new bool[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
                glitched[i] = IsGlitchedWord(tokens[i]);

            // Absorb tiny fragments ("x", "%", "»") that sit next to glitched words.
            for (int i = 0; i < tokens.Count; i++)
            {
                if (glitched[i] || !IsFragment(tokens[i]))
                    continue;
                bool left = i > 0 && glitched[i - 1];
                bool right = i + 1 < tokens.Count && glitched[i + 1];
                if (left || right)
                    glitched[i] = true;
            }

            var sb = new StringBuilder();
            bool inRun = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!glitched[i])
                {
                    inRun = false;
                    Append(sb, tokens[i]);
                    continue;
                }

                // A real word may have glitch characters glued to its end ("understanŖ¼ŕ").
                string prefix = CleanPrefix(tokens[i]);
                if (prefix != null)
                {
                    Append(sb, prefix);
                    inRun = false;
                }
                if (!inRun)
                {
                    Append(sb, marker);
                    inRun = true;
                }
            }
            return sb.ToString();
        }

        /// <summary>The readable start of a glitched word, if it begins with at least 3 plain letters.</summary>
        private static string CleanPrefix(string token)
        {
            int length = 0;
            while (length < token.Length && !IsRare(token[length]) && char.IsLetter(token[length]))
                length++;
            return length >= 3 ? token.Substring(0, length) : null;
        }

        private static bool IsFragment(string token)
        {
            if (token.Length > 2)
                return false;
            foreach (char c in token)
            {
                if (char.IsLetterOrDigit(c) && c <= 0x7F && c != 'x')
                    return false;
            }
            return true;
        }

        private static void Append(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(value);
        }
    }
}
