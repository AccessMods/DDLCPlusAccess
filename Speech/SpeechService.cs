using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityAccessibilityLib;

namespace DDLCPlusAccess
{
    /// <summary>
    /// The mod's single speech service. Everything the mod says goes through <see cref="Output"/>,
    /// <see cref="Announce"/> or <see cref="RepeatLast"/>; only this class talks to Prism.
    ///
    /// A dedicated thread owns Prism (a Prism backend is not thread-safe, and a slow screen-reader
    /// call must never freeze a game frame). Callers only enqueue. Text is cleaned with
    /// UnityAccessibilityLib's TextCleaner, duplicates within half a second are dropped, and the last
    /// dialogue line is kept for the repeat key - the same behaviour the mod had before.
    /// </summary>
    public static class SpeechService
    {
        /// <summary>Folder (inside Mods) that holds prism.dll and its licence files.</summary>
        public const string NativeFolderName = "DDLCPlusAccess";

        private const double DuplicateWindowSeconds = 0.5;

        private static readonly BlockingCollection<KeyValuePair<string, bool>> s_Queue =
            new BlockingCollection<KeyValuePair<string, bool>>();

        private static Thread s_Thread;
        private static volatile bool s_Ready;
        private static IntPtr s_Backend = IntPtr.Zero;

        private static string s_LastMessage = "";
        private static DateTime s_LastMessageTime = DateTime.MinValue;

        private static string s_RepeatSpeaker = "";
        private static string s_RepeatText = "";
        private static int s_RepeatType;

        public static Dictionary<int, string> TextTypeNames { get; set; }
        public static Func<int, bool> ShouldStoreForRepeat { get; set; }

        /// <summary>True once Prism has a working backend.</summary>
        public static bool IsReady => s_Ready;

        /// <summary>Starts the speech thread, which loads Prism. Safe to call more than once.</summary>
        public static void Start()
        {
            if (s_Thread != null)
                return;

            s_Thread = new Thread(Run) { IsBackground = true, Name = "DDLCPlusAccess speech" };
            s_Thread.Start();
        }

        /// <summary>Speaks text, prefixed with the speaker's name for dialogue.</summary>
        public static void Output(string speaker, string text, int textType = GameTextType.Dialogue)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string formatted = Format(speaker, text, textType);
            if (string.IsNullOrWhiteSpace(formatted))
                return;

            lock (s_Queue)
            {
                DateTime now = DateTime.UtcNow;
                if (
                    formatted == s_LastMessage
                    && (now - s_LastMessageTime).TotalSeconds < DuplicateWindowSeconds
                )
                    return;
                s_LastMessage = formatted;
                s_LastMessageTime = now;

                if (ShouldStoreForRepeat != null && ShouldStoreForRepeat(textType))
                {
                    s_RepeatSpeaker = speaker ?? "";
                    s_RepeatText = text;
                    s_RepeatType = textType;
                }
            }

            string typeName =
                TextTypeNames != null && TextTypeNames.TryGetValue(textType, out string name)
                    ? name
                    : textType.ToString();
            ScreenReaderMod.Logger?.Msg($"[{typeName}] {formatted}");

            Enqueue(formatted, interrupt: false);
        }

        /// <summary>Speaks text without a speaker name.</summary>
        public static void Announce(string text, int textType = GameTextType.SystemMessage)
        {
            Output(null, text, textType);
        }

        /// <summary>Repeats the last dialogue/narration line, interrupting current speech.</summary>
        public static void RepeatLast()
        {
            string text;
            lock (s_Queue)
            {
                text = string.IsNullOrWhiteSpace(s_RepeatText)
                    ? null
                    : Format(s_RepeatSpeaker, s_RepeatText, s_RepeatType);
            }
            if (text == null)
            {
                ScreenReaderMod.Logger?.Msg("Nothing to repeat");
                return;
            }
            Enqueue(text, interrupt: true);
        }

        /// <summary>Speaks immediately, cutting off current speech (answers to a key press).</summary>
        public static void SpeakInterrupting(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                Enqueue(TextCleaner.Clean(text), interrupt: true);
        }

        private static string Format(string speaker, string text, int textType)
        {
            string cleaned = TextCleaner.Clean(text);
            if (textType == GameTextType.Dialogue && !string.IsNullOrWhiteSpace(speaker))
                return speaker + ": " + cleaned;
            return cleaned;
        }

        private static void Enqueue(string text, bool interrupt)
        {
            if (!s_Queue.IsAddingCompleted)
                s_Queue.Add(new KeyValuePair<string, bool>(text, interrupt));
        }

        // ------------------------------------------------------------------ speech thread

        private static void Run()
        {
            try
            {
                if (!InitializePrism())
                    return;
            }
            catch (Exception ex)
            {
                ScreenReaderMod.Logger?.Error($"Prism failed to start: {ex}");
                return;
            }

            foreach (var item in s_Queue.GetConsumingEnumerable())
            {
                try
                {
                    int result = PrismNative.prism_backend_output(
                        s_Backend,
                        PrismNative.ToUtf8(item.Key),
                        item.Value
                    );
                    if (result != PrismNative.PRISM_OK)
                    {
                        // On NVDA/JAWS the error may come from the braille half after the line
                        // was already spoken; log it and move on rather than resend.
                        ScreenReaderMod.Logger?.Warning(
                            $"Prism output failed ({PrismNative.ErrorString(result)}), interrupt={item.Value}: {item.Key}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    ScreenReaderMod.Logger?.Error($"Prism output error: {ex.Message}");
                }
            }
        }

        private static bool InitializePrism()
        {
            string modsFolder = Path.GetDirectoryName(typeof(SpeechService).Assembly.Location);
            string dllPath = Path.Combine(Path.Combine(modsFolder, NativeFolderName), "prism.dll");

            if (!File.Exists(dllPath))
            {
                ScreenReaderMod.Logger?.Error($"prism.dll not found at {dllPath}. Speech is disabled.");
                return false;
            }
            if (!PrismNative.Load(dllPath, out int win32Error))
            {
                ScreenReaderMod.Logger?.Error(
                    $"Could not load {dllPath} (Win32 error {win32Error}). Speech is disabled."
                );
                return false;
            }

            string version = PrismNative.FromUtf8(PrismNative.prism_version_string());

            IntPtr context = PrismNative.prism_init(IntPtr.Zero);
            if (context == IntPtr.Zero)
            {
                ScreenReaderMod.Logger?.Error("prism_init failed. Speech is disabled.");
                return false;
            }

            s_Backend = PrismNative.prism_registry_create_best(context);
            if (s_Backend == IntPtr.Zero)
            {
                ScreenReaderMod.Logger?.Error(
                    "Prism found no working speech backend (no screen reader or Windows voice). Speech is disabled."
                );
                return false;
            }

            string backend = PrismNative.FromUtf8(PrismNative.prism_backend_name(s_Backend));
            ScreenReaderMod.Logger?.Msg($"Prism {version} loaded from {dllPath}; speaking through {backend}.");
            s_Ready = true;
            return true;
        }
    }
}
