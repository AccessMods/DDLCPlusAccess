using System.Collections.Generic;
using RenPyParser.Screens.PoemScreen;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Describes what a poem looks like when its appearance carries meaning the text alone
    /// does not (glitched or garbled poems, unusual layouts, stained paper). The poem is
    /// recognized from its raw (English) data, which is the same in every language.
    /// </summary>
    public static class PoemVisualDescriptions
    {
        /// <summary>
        /// Returns a localized description for the poem and the paper it is shown on,
        /// or null if it looks like an ordinary poem.
        /// </summary>
        public static string Describe(Poem poem, string paper)
        {
            if (poem == null)
                return null;

            var parts = new List<string>();

            string poemKey = GetPoemKey(poem);
            if (poemKey != null)
                parts.Add(Loc.Get(poemKey));

            string paperKey = GetPaperKey(paper);
            if (paperKey != null)
                parts.Add(Loc.Get(paperKey));

            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        private static string GetPoemKey(Poem poem)
        {
            string author = poem.author ?? "";
            string title = (poem.title ?? "").Trim();
            string text = poem.text ?? "";

            // Yuri's last poem in Act 2: jumbled words in a frantic scrawl
            if (poem.yuri_3)
                return "poem.desc_y23";

            // Yuri's "Wheel": one unbroken paragraph
            if (poem.yuri_2)
                return "poem.desc_y22";

            // Sayori's "%" ("Get out of my head")
            if (author == "sayori" && title == "%")
                return "poem.desc_s3";

            // Natsuki's poem replaced by Base64 text ("Open Your Third Eye")
            if (author == "natsuki" && title.StartsWith("T3BlbiBZb3Vy"))
                return "poem.desc_n2b";

            // Monika's "Save Me": the corrupted Act 2 version and the original
            if (author == "monika" && title == "Save Me")
                return text.Contains("Delete Her") ? "poem.desc_m22" : "poem.desc_m2";

            // Sayori's single line in the side stories
            if (author == "sayori" && title.Length == 0 && text.Contains("Sometimes I want to die"))
                return "poem.desc_ps3";

            return null;
        }

        private static string GetPaperKey(string paper)
        {
            if (string.IsNullOrEmpty(paper))
                return null;

            string p = paper.ToLowerInvariant();
            if (p.Contains("poem_pm1"))
                return "poem.paper_pm1";
            if (p.Contains("poem_y1"))
                return "poem.paper_y1";
            if (p.Contains("poem_y2"))
                return "poem.paper_y2";
            return null;
        }
    }
}
