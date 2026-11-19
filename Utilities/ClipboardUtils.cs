namespace DDLCPlusAccess
{
    /// <summary>
    /// Wrapper around SpeechService that maintains API compatibility
    /// with existing patch files.
    /// </summary>
    public static class ClipboardUtils
    {
        /// <summary>
        /// Repeats the last dialogue or narrator text.
        /// </summary>
        public static void RepeatCurrentDialogue()
        {
            SpeechService.RepeatLast();
        }

        /// <summary>
        /// Outputs game text to the screen reader.
        /// </summary>
        /// <param name="speaker">The speaker name (for dialogue).</param>
        /// <param name="text">The text to speak.</param>
        /// <param name="textType">The type of text being spoken.</param>
        public static void OutputGameText(
            string speaker,
            string text,
            TextType textType = TextType.Dialogue
        )
        {
            if (string.IsNullOrEmpty(text))
                return;

            int mappedType = GameTextType.FromLegacyTextType(textType);

            // For dialogue type, use Output with speaker; otherwise use Announce
            if (textType == TextType.Dialogue && !string.IsNullOrEmpty(speaker))
            {
                SpeechService.Output(speaker, text, mappedType);
            }
            else
            {
                SpeechService.Announce(text, mappedType);
            }
        }

        /// <summary>
        /// Outputs poem text to the screen reader.
        /// </summary>
        /// <param name="text">The poem text to speak.</param>
        public static void OutputPoemText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            SpeechService.Announce(text, GameTextType.Poem);
        }
    }
}
