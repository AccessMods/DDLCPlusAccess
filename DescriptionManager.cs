using System.Text.RegularExpressions;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Localized names and descriptions of gallery images (CGs, wallpapers, poems, backgrounds,
    /// sketches, promos, secrets) and of pictures opened from the Files app.
    /// Strings live in Localization/*.txt as "imgname.&lt;asset&gt;" (short title) and
    /// "img.&lt;asset&gt;" (full description), where &lt;asset&gt; is the asset name in lower case
    /// with every run of other characters replaced by "_". Gallery poems quote the game's own
    /// translation of the poem; special poems are handled by SpecialPoemDescriptions.
    /// </summary>
    public static class DescriptionManager
    {
        // Localized image variants share the base image's description
        private static readonly Regex LanguageSuffix = new Regex(
            @"_(en-us|spa-m9|spa|fr-fr|de-de|it-it|pt-br|pyc|ko|ja|zh-cn|zh-tw)\d?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public static string NormalizeKey(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return "";
            string name = LanguageSuffix.Replace(imageName.Trim(), "");
            return Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
        }

        /// <summary>Full localized description of an image, or "" if there is none.</summary>
        public static string GetDescription(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return "";

            // Special poems
            string special = SpecialPoemDescriptions.GetDescription(imageName);
            if (!string.IsNullOrEmpty(special))
                return special;

            string key = "img." + NormalizeKey(imageName);
            return Loc.Has(key) ? Loc.Get(key) : "";
        }

        /// <summary>Short localized title of an image, or null if there is none.</summary>
        public static string GetTitle(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;
            string key = "imgname." + NormalizeKey(imageName);
            return Loc.Has(key) ? Loc.Get(key) : null;
        }

        /// <summary>
        /// Check if an image has a description available
        /// </summary>
        public static bool HasDescription(string imageName)
        {
            return !string.IsNullOrEmpty(GetDescription(imageName));
        }
    }
}
