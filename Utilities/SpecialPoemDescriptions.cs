using System.Text.RegularExpressions;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Localized descriptions of the special poem images (poem_special1..11, poem_end,
    /// poem_end_clearall) and their gallery entries. The texts live in Localization/*.txt
    /// under "poem.special*" / "poem.end*"; each quotes the game's own translation of the
    /// text printed on the image.
    /// </summary>
    public static class SpecialPoemDescriptions
    {
        // Localized image variants: poem_special1_spa, poem_special3_spa-M9, poem_special9_th_ja,
        // poem_end_clearall_ko_2, poem_end_clearall_fr-fr2, poem_special8_original, ...
        private static readonly Regex LanguageSuffix = new Regex(
            @"_(en-us|spa-m9|spa|fr-fr|de-de|it-it|pt-br|pyc|ko|ja|zh-cn|zh-tw)(_?\d)?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex SpecialNumber = new Regex(
            @"^(?:poem|gallery)_special(\d+[ab]?)$",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Returns the localization key describing this image, or null if it is not a
        /// special poem.
        /// </summary>
        public static string GetKey(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
                return null;

            string name = assetName.Trim().ToLowerInvariant();
            int slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name.Substring(slash + 1);
            if (name.EndsWith(".png") || name.EndsWith(".jpg"))
                name = name.Substring(0, name.Length - 4);

            name = LanguageSuffix.Replace(name, "");
            if (name.EndsWith("_th"))
                name = name.Substring(0, name.Length - "_th".Length);
            if (name.EndsWith("_original"))
                name = name.Substring(0, name.Length - "_original".Length);

            if (name == "poem_end" || name == "gallery_final_message")
                return "poem.end";
            if (name == "poem_end_clearall")
                return "poem.end_clearall";

            Match match = SpecialNumber.Match(name);
            return match.Success ? "poem.special" + match.Groups[1].Value : null;
        }

        /// <summary>True if the image is a special poem (whether or not it has a description).</summary>
        public static bool IsSpecialPoem(string assetName)
        {
            return GetKey(assetName) != null;
        }

        /// <summary>The localized description of a special poem image, or null.</summary>
        public static string GetDescription(string assetName)
        {
            string key = GetKey(assetName);
            if (key == null || !Loc.Has(key))
                return null;
            return Loc.Get(key);
        }
    }
}
