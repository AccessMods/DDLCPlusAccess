using System.Reflection;
using HarmonyLib;
using RenpyParser;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DDLCPlusAccess
{
    [HarmonyPatch]
    public static class MenuPatches
    {
        [HarmonyPatch(typeof(RenpyChoiceMenuUI), "ShowChoiceMenu")]
        [HarmonyPostfix]
        public static void RenpyChoiceMenuUI_ShowChoiceMenu_Postfix(
            RenpyChoiceMenuUI __instance,
            System.Collections.Generic.List<RenpyChoiceEntryUI> entries,
            int activeCount
        )
        {
            try
            {
                ScreenReaderMod.Logger?.Msg($"Choice menu shown with {activeCount} choices");

                ClipboardUtils.OutputGameText(
                    "",
                    activeCount == 1
                        ? Loc.Get("ui.choose_one")
                        : Loc.Get("ui.choose_many", activeCount),
                    TextType.Menu
                );
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyChoiceMenuUI_ShowChoiceMenu_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpyMainMenuUI), "OnShow")]
        [HarmonyPostfix]
        public static void RenpyMainMenuUI_OnShow_Postfix(RenpyMainMenuUI __instance)
        {
            try
            {
                ScreenReaderMod.Logger?.Msg("Main menu shown");
                SceneDescriber.ResetState();

                string artKey = GetMainMenuArtKey();
                if (artKey == "menu.ghost")
                {
                    // The ghost menu has no buttons: describe it every time.
                    SceneDescriber.Describe(artKey);
                    return;
                }

                ClipboardUtils.OutputGameText("", Loc.Get("ui.main_menu"), TextType.Menu);

                // Describe the artwork the first time each version of it is seen.
                if (artKey != null && s_DescribedMenuArt.Add(artKey))
                    SceneDescriber.Describe(artKey);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyMainMenuUI_OnShow_Postfix: {ex.Message}"
                );
            }
        }

        private static readonly System.Collections.Generic.HashSet<string> s_DescribedMenuArt =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>
        /// Mirrors RenpyMainMenuUI.OnShow: which artwork is beside the menu in the current act.
        /// </summary>
        private static string GetMainMenuArtKey()
        {
            var context = Renpy.CurrentContext;
            if (context == null)
                return null;

            if (context.GetVariableFloat("persistent.ghost_menu") == 1f)
                return "menu.ghost";
            if (Renpy.CurrentLabel == "yuri_kill_2")
                return "menu.art_none";

            float playthrough = context.GetVariableFloat("persistent.playthrough");
            if (playthrough == 1f || playthrough == 2f)
                return "menu.art_act2";
            if (playthrough == 4f)
                return "menu.art_act4";
            return "menu.art_normal";
        }

        private static bool IsInMainMenuDuringAct2()
        {
            try
            {
                return RenpyScreenManager.Instance != null
                    && RenpyScreenManager.Instance.InMainMenu
                    && Renpy.CurrentContext?.GetVariableFloat("persistent.playthrough") == 1f;
            }
            catch
            {
                return false;
            }
        }

        // Ghost menu: any key flashes Monika's ghost, then the game returns to the launcher.
        [HarmonyPatch(typeof(RenpyMainMenuUI), "FlashGhostMonika")]
        [HarmonyPrefix]
        public static void RenpyMainMenuUI_FlashGhostMonika_Prefix()
        {
            try
            {
                SceneDescriber.Describe("menu.ghost_flash");
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error($"Error in FlashGhostMonika prefix: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(RenpyPreferencesMenuUI), "OnShow")]
        [HarmonyPostfix]
        public static void RenpyPreferencesMenuUI_OnShow_Postfix(RenpyPreferencesMenuUI __instance)
        {
            try
            {
                ScreenReaderMod.Logger?.Msg("Preferences menu shown");
                ClipboardUtils.OutputGameText("", Loc.Get("ui.preferences_menu"), TextType.Menu);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyPreferencesMenuUI_OnShow_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpySaveLoadScreen), "OnShow")]
        [HarmonyPostfix]
        public static void RenpySaveLoadScreen_OnShow_Postfix(RenpySaveLoadScreen __instance)
        {
            try
            {
                ScreenReaderMod.Logger?.Msg("Save/Load screen shown");
                ClipboardUtils.OutputGameText("", Loc.Get("ui.save_load_menu"), TextType.Menu);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpySaveLoadScreen_OnShow_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpyConfirmScreenUI), "SetData")]
        [HarmonyPostfix]
        public static void RenpyConfirmScreenUI_SetData_Postfix(
            RenpyConfirmScreenUI __instance,
            string messageStr
        )
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(messageStr))
                {
                    string processedMessage = TextHelper.CleanText(messageStr);
                    ScreenReaderMod.Logger?.Msg($"Confirmation dialog message: {processedMessage}");
                    ClipboardUtils.OutputGameText("", processedMessage, TextType.SystemMessage);
                }
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyConfirmScreenUI_SetData_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpyNameInputScreenUI), "SetData")]
        [HarmonyPostfix]
        public static void RenpyNameInputScreenUI_SetData_Postfix(
            RenpyNameInputScreenUI __instance,
            string messageStr
        )
        {
            try
            {
                ScreenReaderMod.Logger?.Msg($"Name input screen shown with message: {messageStr}");
                ClipboardUtils.OutputGameText("", messageStr, TextType.SystemMessage);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyNameInputScreenUI_SetData_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpyButtonUI), "OnButtonFocussed")]
        [HarmonyPostfix]
        public static void RenpyButtonUI_OnButtonFocussed_Postfix(RenpyButtonUI __instance)
        {
            try
            {
                // Check if this is a RenpyToggleButtonUI with a preference type (settings)
                if (
                    __instance is RenpyToggleButtonUI toggleButton
                    && toggleButton.PreferenceType.HasValue
                )
                {
                    string settingName = PreferenceTypeNames.GetFriendlyName(
                        toggleButton.PreferenceType.Value
                    );
                    string currentState = PreferenceTypeNames.FormatToggleState(toggleButton.isOn);
                    string announcement = $"{settingName}, {currentState}";

                    ScreenReaderMod.Logger?.Msg($"Toggle focused: {announcement}");
                    ClipboardUtils.OutputGameText("", announcement, TextType.Settings);
                }
                // Regular button handling (existing functionality)
                else if (
                    __instance.textObject != null
                    && !string.IsNullOrWhiteSpace(__instance.textObject.text)
                )
                {
                    string buttonText = __instance.textObject.text;
                    ScreenReaderMod.Logger?.Msg($"Button focused: {buttonText}");

                    // Act 2's main menu shows "New Game" as garbled symbols.
                    if (GlitchText.ContainsGlitch(buttonText))
                    {
                        buttonText = IsInMainMenuDuringAct2()
                            ? Loc.Get("ui.new_game_glitched")
                            : Loc.Get("ui.glitched_button");
                    }
                    ClipboardUtils.OutputGameText("", buttonText, TextType.Menu);
                }
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyButtonUI_OnButtonFocussed_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpySliderUI), "OnSliderFocussed")]
        [HarmonyPostfix]
        public static void RenpySliderUI_OnSliderFocussed_Postfix(RenpySliderUI __instance)
        {
            try
            {
                string settingName = PreferenceTypeNames.GetFriendlyName(__instance.PreferenceType);
                string currentValue = PreferenceTypeNames.FormatSliderValue(
                    __instance.Value,
                    __instance.PreferenceType
                );
                string announcement = $"{settingName}, {currentValue}";

                ScreenReaderMod.Logger?.Msg($"Slider focused: {announcement}");
                ClipboardUtils.OutputGameText("", announcement, TextType.Settings);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpySliderUI_OnSliderFocussed_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpySliderUI), "SliderValueChanged")]
        [HarmonyPostfix]
        public static void RenpySliderUI_SliderValueChanged_Postfix(
            RenpySliderUI __instance,
            UnityEngine.UI.Slider s
        )
        {
            try
            {
                if (!s.interactable)
                    return;

                // Only announce slider value changes when the slider is actually focused/selected
                if (
                    EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject != s.gameObject
                )
                    return;

                string newValue = PreferenceTypeNames.FormatSliderValue(
                    s.value,
                    __instance.PreferenceType
                );

                ScreenReaderMod.Logger?.Msg($"Slider value changed: {newValue}");
                ClipboardUtils.OutputGameText("", newValue, TextType.Settings);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpySliderUI_SliderValueChanged_Postfix: {ex.Message}"
                );
            }
        }

        [HarmonyPatch(typeof(RenpyToggleButtonUI), "OnValueChanged")]
        [HarmonyPostfix]
        public static void RenpyToggleButtonUI_OnValueChanged_Postfix(
            RenpyToggleButtonUI __instance,
            bool newValue
        )
        {
            try
            {
                if (!__instance.PreferenceType.HasValue)
                    return;

                string newState = PreferenceTypeNames.FormatToggleState(newValue);

                ScreenReaderMod.Logger?.Msg($"Toggle value changed: {newState}");
                ClipboardUtils.OutputGameText("", newState, TextType.Settings);
            }
            catch (System.Exception ex)
            {
                ScreenReaderMod.Logger?.Error(
                    $"Error in RenpyToggleButtonUI_OnValueChanged_Postfix: {ex.Message}"
                );
            }
        }
    }
}
