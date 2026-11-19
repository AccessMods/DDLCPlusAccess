using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using MelonLoader;
using UnityAccessibilityLib;
using UnityEngine;

namespace DDLCPlusAccess
{
    public class ScreenReaderMod : MelonMod
    {
        public static MelonLogger.Instance Logger { get; private set; }
        public static ScreenReaderMod Instance { get; private set; }
        private static bool _isInitialized = false;
        private static bool _announcedLoaded = false;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Logger = LoggerInstance;
            Logger.Msg("DDLC Screen Reader Mod initialized!");

            // Prism runs on its own thread; starting it here (not on a loader callback) is safe.
            SpeechService.TextTypeNames = GameTextType.GetTextTypeNames();
            SpeechService.ShouldStoreForRepeat = GameTextType.ShouldStoreForRepeat;
            SpeechService.Start();
        }

        private void InitializeSpeechSystem()
        {
            if (_isInitialized)
                return;

            try
            {
                // Set up accessibility library logging
                AccessibilityLog.Logger = new MelonLoggerAdapter(LoggerInstance);

                // Register Ren'Py/TMP-specific text cleaning patterns
                RegisterTextCleanerPatterns();

                // Initialize special poem descriptions
                SpecialPoemDescriptions.LoadDescriptions();
                Logger.Msg(
                    $"Special poem descriptions loaded: {SpecialPoemDescriptions.DescriptionCount} entries"
                );

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize speech system: {ex.Message}");
            }
        }

        private void RegisterTextCleanerPatterns()
        {
            // Ren'Py curly brace tags: {w}, {nw}, {clear}, {fast}, {cps=...}, etc.
            TextCleaner.AddRegexReplacement(@"\{[^}]*\}", "");

            // TMP square bracket tags
            TextCleaner.AddRegexReplacement(
                @"\[color=[^\]]*\]|\[/color\]",
                "",
                RegexOptions.IgnoreCase
            );
            TextCleaner.AddRegexReplacement(
                @"\[size=[^\]]*\]|\[/size\]",
                "",
                RegexOptions.IgnoreCase
            );
            TextCleaner.AddRegexReplacement(@"\[b\]|\[/b\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[i\]|\[/i\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[u\]|\[/u\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[s\]|\[/s\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[center\]|\[/center\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[right\]|\[/right\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(@"\[left\]|\[/left\]", "", RegexOptions.IgnoreCase);
            TextCleaner.AddRegexReplacement(
                @"\[font=[^\]]*\]|\[/font\]",
                "",
                RegexOptions.IgnoreCase
            );
            TextCleaner.AddRegexReplacement(
                @"\[alpha=[^\]]*\]|\[/alpha\]",
                "",
                RegexOptions.IgnoreCase
            );

            // Special replacements
            TextCleaner.AddReplacement(
                @"<sprite name=""keyboard_enter"">Apply",
                "Press Enter to Apply"
            );
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Logger.Msg($"Scene loaded: {sceneName} (Index: {buildIndex})");

            // Initialize speech system on first scene load (when Unity is ready)
            InitializeSpeechSystem();

            // Proof of life, once the game's language is known (it is not yet during the preload scene).
            if (!_announcedLoaded && sceneName != "LauncherPreload")
            {
                _announcedLoaded = true;
                SpeechService.Announce(Loc.Get("ui.mod_loaded"), GameTextType.SystemMessage);
            }

            // Announce scene changes
            if (sceneName == "LauncherScene")
            {
                ClipboardUtils.OutputGameText("", Loc.Get("ui.launcher"), TextType.Menu);
            }
        }

        public override void OnUpdate()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    ClipboardUtils.RepeatCurrentDialogue();
                }

                // D: repeat the last scene description
                if (Input.GetKeyDown(KeyCode.D))
                {
                    SceneDescriber.RepeatLastDescription();
                }

                if (Input.GetKeyDown(KeyCode.C) && IsInSettingsApp())
                {
                    AnnounceDataCollectionPercentage();
                }

                if (Input.GetKeyDown(KeyCode.P) && IsInJukeboxApp())
                {
                    AnnounceJukeboxPosition();
                }

                // History screen navigation
                if (HistoryPatches.IsHistoryScreenActive)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        HistoryPatches.NavigateToPrevious();
                    }
                    else if (Input.GetKeyDown(KeyCode.DownArrow))
                    {
                        HistoryPatches.NavigateToNext();
                    }
                }

                // File viewer navigation
                if (FileContentPatches.IsFileViewerActive)
                {
                    bool ctrlHeld =
                        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

                    if (ctrlHeld && Input.GetKeyDown(KeyCode.Home))
                    {
                        FileContentPatches.NavigateToFirst();
                    }
                    else if (ctrlHeld && Input.GetKeyDown(KeyCode.End))
                    {
                        FileContentPatches.NavigateToLast();
                    }
                    else if (Input.GetKeyDown(KeyCode.UpArrow))
                    {
                        FileContentPatches.NavigateToPrevious();
                    }
                    else if (Input.GetKeyDown(KeyCode.DownArrow))
                    {
                        FileContentPatches.NavigateToNext();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnUpdate: {ex.Message}");
            }
        }

        private void AnnounceDataCollectionPercentage()
        {
            try
            {
                if (UnlockSystem.Instance != null)
                {
                    float percentage = UnlockSystem.Instance.UnlockedPercentage;
                    int roundedPercentage = Mathf.RoundToInt(percentage);
                    string message = Loc.Get("ui.data_collected", roundedPercentage);

                    Logger?.Msg($"Data collection percentage requested: {message}");
                    ClipboardUtils.OutputGameText("", message, TextType.SystemMessage);
                }
                else
                {
                    Logger?.Warning(
                        "UnlockSystem.Instance is null - cannot retrieve data collection percentage"
                    );
                    ClipboardUtils.OutputGameText(
                        "",
                        Loc.Get("ui.data_unavailable"),
                        TextType.SystemMessage
                    );
                }
            }
            catch (Exception ex)
            {
                Logger?.Error($"Error announcing data collection percentage: {ex.Message}");
                ClipboardUtils.OutputGameText(
                    "",
                    Loc.Get("ui.data_error"),
                    TextType.SystemMessage
                );
            }
        }

        private bool IsInSettingsApp()
        {
            try
            {
                var settingsApp = UnityEngine.Object.FindObjectOfType<RenpyLauncher.SettingsApp>();
                return settingsApp != null && settingsApp.gameObject.activeInHierarchy;
            }
            catch (System.Exception ex)
            {
                Logger?.Error($"Error checking if in settings app: {ex.Message}");
                return false;
            }
        }

        private bool IsInJukeboxApp()
        {
            try
            {
                var jukeboxApp = UnityEngine.Object.FindObjectOfType<JukeboxApp>();
                if (jukeboxApp == null)
                    return false;

                // Check if the jukebox app is actually open and active
                // The Window component should be active when the app is open
                return jukeboxApp.gameObject.activeInHierarchy
                    && jukeboxApp.Window != null
                    && jukeboxApp.Window.activeInHierarchy;
            }
            catch (System.Exception ex)
            {
                Logger?.Error($"Error checking if in jukebox app: {ex.Message}");
                return false;
            }
        }

        private void AnnounceJukeboxPosition()
        {
            try
            {
                var jukeboxApp = UnityEngine.Object.FindObjectOfType<JukeboxApp>();
                if (jukeboxApp?.JukeboxPlayer == null)
                {
                    ClipboardUtils.OutputGameText("", Loc.Get("ui.jukebox_none"), TextType.Jukebox);
                    return;
                }

                var currentTrack = jukeboxApp.JukeboxPlayer.GetCurrentTrack();
                if (!currentTrack.CurrentTrackValid())
                {
                    ClipboardUtils.OutputGameText("", Loc.Get("ui.jukebox_no_track"), TextType.Jukebox);
                    return;
                }

                var trackInfo = jukeboxApp.JukeboxPlayer.GetCurrentTrackEntry();
                if (trackInfo == null)
                {
                    ClipboardUtils.OutputGameText(
                        "",
                        Loc.Get("ui.jukebox_no_info"),
                        TextType.Jukebox
                    );
                    return;
                }

                var playerState = jukeboxApp.JukeboxPlayer.GetPlayerState();
                if (playerState == JukeboxPlayer.PlayerState.Stopped)
                {
                    ClipboardUtils.OutputGameText(
                        "",
                        Loc.Get("ui.jukebox_stopped", trackInfo.TrackName),
                        TextType.Jukebox
                    );
                    return;
                }

                float currentPosition = jukeboxApp.JukeboxPlayer.GetCurrentTrackProgress();
                float totalLength = trackInfo.Length;

                // Convert to minutes:seconds format
                int currentMinutes = (int)Math.Floor(currentPosition / 60f);
                int currentSeconds = (int)Math.Floor(currentPosition - (float)currentMinutes * 60f);
                int totalMinutes = (int)Math.Floor(totalLength / 60f);
                int totalSeconds = (int)Math.Floor(totalLength - (float)totalMinutes * 60f);

                string positionText = $"{currentMinutes}:{currentSeconds:D2}";
                string totalText = $"{totalMinutes}:{totalSeconds:D2}";

                string message = Loc.Get(
                    "ui.jukebox_position",
                    trackInfo.TrackName,
                    positionText,
                    totalText
                );

                Logger?.Msg($"Jukebox position requested: {message}");
                ClipboardUtils.OutputGameText("", message, TextType.Jukebox);
            }
            catch (Exception ex)
            {
                Logger?.Error($"Error announcing jukebox position: {ex.Message}");
                ClipboardUtils.OutputGameText(
                    "",
                    Loc.Get("ui.jukebox_error"),
                    TextType.Jukebox
                );
            }
        }

        public override void OnDeinitializeMelon()
        {
            // Prism is left to die with the game process: tearing down its COM/RPC state at exit
            // can crash the game.
            Logger.Msg("Screen Reader Mod deinitialized.");
        }

        public override void OnApplicationQuit()
        {
            Logger.Msg("DDLC Application quitting - Screen Reader Mod cleanup");
        }
    }
}
