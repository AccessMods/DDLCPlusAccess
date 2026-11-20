using System.Collections.Generic;

namespace DDLCPlusAccess
{
    public static class SecretDescriptions
    {
        public static readonly Dictionary<string, string> Descriptions = new Dictionary<
            string,
            string
        >
        {
            // Secret/special image descriptions
            // Format: ["asset_name"] = "Description of the secret image",
            // Special poems (gallery_special*) are described by SpecialPoemDescriptions (localized).
            ["gallery_ghost_menu"] =
                @"
Four female characters are depicted in a high-contrast, black-and-white art style against a plain white background. Their bodies are rendered as mostly solid black silhouettes with some white outlines and details. All four characters have wide, open mouths and their eyes are solid black circles without pupils or irises. The character on the far right is in the foreground, reaching one hand out towards the viewer.
",
        };
    }
}
