using DDLC.Screens.PoetryGameScreen;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace DDLCPlusAccess
{
    [HarmonyPatch]
    public static class PoetryPatches
    {
        [HarmonyPatch(typeof(WordSelect), "OnSelect")]
        [HarmonyPostfix]
        public static void WordSelect_OnSelect_Postfix(
            WordSelect __instance,
            BaseEventData eventData
        )
        {
            try
            {
                var word = __instance.Word;
                string wordText = DescribeWord(word);

                if (!string.IsNullOrWhiteSpace(wordText))
                {
                    ScreenReaderMod.Logger?.Msg($"Poetry word selected: '{wordText}'");

                    ClipboardUtils.OutputGameText("", wordText, TextType.PoetryGame);
                }
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in WordSelect_OnSelect_Postfix: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// The spoken form of a poem word. Glitched words (Act 2) and Act 3's corrupted
        /// "Monika" words would otherwise be read as random symbols.
        /// </summary>
        private static string DescribeWord(PoetryGameScreen.SelectableWord word)
        {
            if (word.GlitchWord)
                return Loc.Get("poetry.glitch_word");
            if (GetCurrentPlaythrough() == 3)
                return Loc.Get("poetry.monika_word");
            return word.Word.word;
        }

        private static readonly System.Collections.Generic.HashSet<string> s_DescribedStickers =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Character stickers hop when you pick a word. Most hops are already conveyed by
        /// "X likes this"; describe only the unusual Act 2 stickers, once per poem.
        /// </summary>
        [HarmonyPatch(typeof(RenpyMainBase), "Show", typeof(RenpyParser.Show))]
        [HarmonyPostfix]
        public static void RenpyMainBase_Show_Postfix(RenpyParser.Show show)
        {
            try
            {
                if (show == null || string.IsNullOrEmpty(show.ImageName))
                    return;

                string name = string.IsNullOrEmpty(show.Variant)
                    ? show.ImageName
                    : show.ImageName + " " + show.Variant;

                string key = null;
                switch (name)
                {
                    case "m_sticker hop":
                        key = "poetry.sticker_monika";
                        break;
                    case "y_sticker_cut":
                    case "y_sticker_cut hop":
                        key = "poetry.sticker_yuri_cut";
                        break;
                    case "y_sticker hopg":
                    case "y_sticker_cut hopg":
                        key = "poetry.sticker_yuri_creepy";
                        break;
                }

                if (key != null && s_DescribedStickers.Add(key))
                    SceneDescriber.Describe(key);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error($"Error in RenpyMainBase_Show_Postfix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(WordSelect), "OnPointerEnter")]
        [HarmonyPostfix]
        public static void WordSelect_OnPointerEnter_Postfix(
            WordSelect __instance,
            PointerEventData eventData
        )
        {
            try
            {
                var word = __instance.Word;
                string wordText = DescribeWord(word);

                if (!string.IsNullOrWhiteSpace(wordText))
                {
                    ScreenReaderMod.Logger?.Msg($"Poetry word hovered: '{wordText}'");

                    ClipboardUtils.OutputGameText("", wordText, TextType.PoetryGame);
                }
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in WordSelect_OnPointerEnter_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(PoetryGameScreen), "BeginPoetryGame")]
        [HarmonyPostfix]
        public static void PoetryGameScreen_BeginPoetryGame_Postfix(PoetryGameScreen __instance)
        {
            try
            {
                s_DescribedStickers.Clear();
                ClipboardUtils.OutputGameText("", Loc.Get("poetry.start"), TextType.SystemMessage);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in PoetryGameScreen_BeginPoetryGame_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(PoetryGameScreen), "OnWordSelected")]
        [HarmonyPrefix]
        public static void PoetryGameScreen_OnWordSelected_Prefix(
            PoetryGameScreen __instance,
            WordSelect selectedWord
        )
        {
            try
            {
                var word = selectedWord.Word;
                string wordText = word.Word.word;

                // Get current progress before it's incremented
                int currentProgress = (int)
                    __instance
                        .GetType()
                        .GetField(
                            "Progress",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                        )
                        ?.GetValue(__instance);

                // Get character preferences for this word
                string characterReactions = GetCharacterReactions(word.Word);

                string message = "";

                if (!string.IsNullOrWhiteSpace(characterReactions))
                {
                    message = characterReactions;
                }

                // Check if this is the special Act 2 poem with the glitched counter
                int playthrough = GetCurrentPlaythrough();
                int chapter = (int)
                    System.Math.Floor(Renpy.CurrentContext.GetVariableFloat("chapter"));

                string progressDisplay;
                if (playthrough == 2 && chapter == 2)
                {
                    // Act 2, third poem: display repeated 1s like the visual counter
                    progressDisplay = new string('1', currentProgress);
                }
                else
                {
                    // Normal progress display
                    progressDisplay = currentProgress.ToString();
                }

                message +=
                    (message.Length > 0 ? ". " : "") + Loc.Get("poetry.progress", progressDisplay);

                ClipboardUtils.OutputGameText("", message, TextType.SystemMessage);

                // Picking the rare glitched word (Act 2) breaks the minigame's visuals.
                bool alreadyGlitched = HarmonyLib
                    .Traverse.Create(__instance)
                    .Field("PoemGameGlitch")
                    .GetValue<bool>();
                if (word.GlitchWord && !alreadyGlitched)
                    SceneDescriber.Describe("poetry.glitch_selected");
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in PoetryGameScreen_OnWordSelected_Prefix: {ex.Message}"
                );
            }
        }

        private static string GetCharacterReactions(
            RenPyParser.VGPrompter.DataHolders.PoetryWord poetryWord
        )
        {
            var reactions = new System.Collections.Generic.List<string>();

            // Get current playthrough to determine which characters are available
            int playthrough = GetCurrentPlaythrough();

            if (playthrough == 0)
            {
                // Act 1: Show all character reactions based on individual favor levels
                if (poetryWord.sayouriFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_sayori"));
                if (poetryWord.natsukiFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_natsuki"));
                if (poetryWord.yuriFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_yuri"));
            }
            else if (playthrough >= 1 && playthrough <= 2)
            {
                // Act 2: Only show Natsuki vs Yuri reactions (Sayori is gone 😭)
                // Based on the game logic: if natsukiFavour > yuriFavour, show Natsuki, else show Yuri
                if (poetryWord.natsukiFavour > poetryWord.yuriFavour)
                {
                    reactions.Add(Loc.Get("poetry.likes_natsuki"));
                }
                else
                {
                    reactions.Add(Loc.Get("poetry.likes_yuri"));
                }
            }
            else if (playthrough == 3)
            {
                // Act 3: Only Monika exists, but she doesn't have favor values in the original poetry word data
                // The game doesn't show reactions in Act 3, so we'll return empty
                return "";
            }
            else if (playthrough == 4)
            {
                // Act 4: Similar to Act 1 but without Monika - though this might not have poetry games
                if (poetryWord.sayouriFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_sayori"));
                if (poetryWord.natsukiFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_natsuki"));
                if (poetryWord.yuriFavour >= 3)
                    reactions.Add(Loc.Get("poetry.likes_yuri"));
            }

            if (reactions.Count == 0)
                return "";

            if (reactions.Count == 1)
                return reactions[0];

            if (reactions.Count == 2)
                return Loc.Get("poetry.and2", reactions[0], reactions[1]);

            // All three like it (only possible in Acts 1 and 4)
            return Loc.Get("poetry.and3", reactions[0], reactions[1], reactions[2]);
        }

        private static int GetCurrentPlaythrough()
        {
            try
            {
                return (int)
                    System.Math.Floor(
                        Renpy.CurrentContext.GetVariableFloat("persistent.playthrough")
                    );
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error($"Error getting playthrough: {ex.Message}");
                return 0; // Default to playthrough 0 if we can't determine it
            }
        }
    }
}
