using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Localized strings for everything the mod itself says (scene descriptions, menu hints, ...).
    /// Strings live in embedded resources named "Loc.&lt;language&gt;.txt" (see Localization/*.txt),
    /// one "key=value" pair per line. The language follows the game's own language setting
    /// (Renpy.GetCurrentLanguagePrefix): en-us, spa, spa-m9, fr-fr, de-de, it-it, pt-br,
    /// pyc (Russian), ko, ja, zh-cn, zh-tw. Missing keys fall back to English, then to the key.
    /// </summary>
    public static class Loc
    {
        public const string DefaultLanguage = "en-us";

        private static readonly Dictionary<string, Dictionary<string, string>> s_Tables =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The game's current language prefix, lower-cased (e.g. "spa-m9").</summary>
        public static string CurrentLanguage
        {
            get
            {
                try
                {
                    string prefix = Renpy.GetCurrentLanguagePrefix();
                    if (!string.IsNullOrEmpty(prefix))
                        return prefix.ToLowerInvariant();
                }
                catch
                {
                    // Language table not loaded yet; fall back to the language enum below.
                }

                try
                {
                    if (s_LanguagePrefixes.TryGetValue((int)Renpy.CurrentLanguage, out string fallback))
                        return fallback;
                }
                catch
                {
                    // Renpy is not initialised yet.
                }
                return DefaultLanguage;
            }
        }

        // Mirrors the game's SupportedLanguages asset (ExtendedSystemLanguage value -> prefix).
        private static readonly Dictionary<int, string> s_LanguagePrefixes = new Dictionary<
            int,
            string
        >
        {
            [10] = "en-us",
            [34] = "spa",
            [43] = "spa-m9",
            [14] = "fr-fr",
            [15] = "de-de",
            [21] = "it-it",
            [28] = "pt-br",
            [30] = "pyc",
            [23] = "ko",
            [22] = "ja",
            [6] = "zh-cn",
            [40] = "zh-cn",
            [41] = "zh-tw",
        };

        /// <summary>Returns the localized string for <paramref name="key"/>, formatted with args.</summary>
        public static string Get(string key, params object[] args)
        {
            string value = Lookup(CurrentLanguage, key) ?? Lookup(DefaultLanguage, key) ?? key;
            if (args == null || args.Length == 0)
                return value;
            try
            {
                return string.Format(value, args);
            }
            catch (FormatException)
            {
                return value;
            }
        }

        /// <summary>True if the key exists in English (the master table).</summary>
        public static bool Has(string key)
        {
            return Lookup(DefaultLanguage, key) != null;
        }

        /// <summary>All keys of a language table (used for diagnostics).</summary>
        public static IEnumerable<string> Keys(string language)
        {
            return GetTable(language).Keys;
        }

        private static string Lookup(string language, string key)
        {
            var table = GetTable(language);
            return table.TryGetValue(key, out string value) ? value : null;
        }

        private static Dictionary<string, string> GetTable(string language)
        {
            lock (s_Tables)
            {
                if (s_Tables.TryGetValue(language, out var table))
                    return table;

                table = Load(language);
                s_Tables[language] = table;
                return table;
            }
        }

        private static Dictionary<string, string> Load(string language)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            string text = ReadResource("Loc." + language + ".txt");
            if (text == null)
            {
                if (!string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                    ScreenReaderMod.Logger?.Warning($"No localization file for '{language}', using English.");
                return table;
            }

            foreach (var entry in ParseKeyValueLines(text))
                table[entry.Key] = Unescape(entry.Value);

            ScreenReaderMod.Logger?.Msg($"Loaded {table.Count} localized strings for '{language}'.");
            return table;
        }

        /// <summary>
        /// Parses "key = value" lines, skipping blank lines and lines starting with '#'.
        /// Only the first '=' separates key from value.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> ParseKeyValueLines(string text)
        {
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == '#')
                        continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0)
                        continue;

                    string key = trimmed.Substring(0, eq).Trim();
                    string value = trimmed.Substring(eq + 1).Trim();
                    yield return new KeyValuePair<string, string>(key, value);
                }
            }
        }

        public static string ReadResource(string logicalName)
        {
            Assembly assembly = typeof(Loc).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream(logicalName))
            {
                if (stream == null)
                    return null;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static string Unescape(string value)
        {
            return value.Replace("\\n", "\n");
        }
    }
}
