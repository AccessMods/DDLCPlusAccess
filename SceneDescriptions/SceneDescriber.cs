using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RenpyParser;

namespace DDLCPlusAccess
{
    /// <summary>
    /// Audio-description layer: announces what is happening on screen (locations, pictures,
    /// glitches, horror events) as the game's script runs.
    ///
    /// Every Ren'Py line the game executes passes through RenpyCallstackEntry.Next, which tells
    /// us the block (label) and the line index. Two data files decide what to say:
    ///   SceneEvents.txt - exact "block:line" triggers for one-off events.
    ///   SceneRules.txt  - generic rules by image / effect name (backgrounds, CGs, sprites).
    /// The descriptions themselves are localized via <see cref="Loc"/>.
    /// </summary>
    [HarmonyPatch]
    public static class SceneDescriber
    {
        private const double DefaultCooldownSeconds = 20;

        private sealed class Trigger
        {
            public string Key;
            public string Argument;
        }

        private sealed class Rule
        {
            public string Key;
            public double CooldownSeconds;
        }

        private static Dictionary<string, List<Trigger>> s_Events;
        private static readonly HashSet<string> s_EventBlocks = new HashSet<string>(
            StringComparer.Ordinal
        );
        private static Dictionary<string, Rule> s_SceneRules;
        private static Dictionary<string, Rule> s_ShowRules;
        private static List<KeyValuePair<Regex, Rule>> s_RegexRules;
        private static Dictionary<string, Rule> s_ExprRules;

        private static readonly Dictionary<string, DateTime> s_LastSpoken =
            new Dictionary<string, DateTime>();

        private static string s_LastBackgroundKey;

        /// <summary>The last description spoken, for the "repeat description" hotkey.</summary>
        public static string LastDescription { get; private set; }

        /// <summary>
        /// True while the game shows dialogue in its "edited" style - the distorted font used when
        /// Monika tampers with the script. Dialogue is prefixed so players know.
        /// </summary>
        public static bool EditedStyleActive { get; private set; }

        // Dialogue tag -> sprite tag, to treat "y 3y5" dialogue variants like "show yuri 3y5".
        private static readonly Dictionary<string, string> s_TagToSprite = new Dictionary<
            string,
            string
        >
        {
            ["s"] = "sayori",
            ["n"] = "natsuki",
            ["y"] = "yuri",
            ["m"] = "monika",
        };

        private static readonly string[] s_ShowKeywords =
        {
            "at",
            "as",
            "zorder",
            "onlayer",
            "behind",
            "with",
        };

        // ------------------------------------------------------------------ Harmony hook

        [HarmonyPatch(typeof(RenpyCallstackEntry), nameof(RenpyCallstackEntry.Next))]
        [HarmonyPostfix]
        public static void RenpyCallstackEntry_Next_Postfix(
            RenpyCallstackEntry __instance,
            Line __result
        )
        {
            if (__result == null)
                return;

            try
            {
                OnLine(__instance, __result);
            }
            catch (Exception ex)
            {
                ScreenReaderMod.Logger?.Error($"SceneDescriber error: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Forget per-scene state (called when returning to the main menu).</summary>
        public static void ResetState()
        {
            s_LastBackgroundKey = null;
            EditedStyleActive = false;
            s_LastSpoken.Clear();
        }

        public static void RepeatLastDescription()
        {
            string text = LastDescription;
            if (string.IsNullOrEmpty(text))
                text = Loc.Get("ui.no_description");
            SpeechService.SpeakInterrupting(text);
        }

        /// <summary>Speaks a localized description and remembers it for the repeat hotkey.</summary>
        public static void Describe(string key, string argument = null)
        {
            string text = argument == null ? Loc.Get(key) : Loc.Get(key, argument);
            if (string.IsNullOrWhiteSpace(text))
                return;

            LastDescription = text;
            s_LastSpoken[key] = DateTime.UtcNow;
            ScreenReaderMod.Logger?.Msg($"Scene description [{key}]");
            SpeechService.Announce(text, GameTextType.Description);
        }

        // ------------------------------------------------------------------ line handling

        private static void OnLine(RenpyCallstackEntry entry, Line line)
        {
            EnsureLoaded();

            string block = entry.blockName;
            int index = entry.nextLine - 1;

            // Track the "edited" (distorted) dialogue style regardless of anything else.
            if (line is RenpyOneLinePython python)
                TrackDialogueStyle(python);

            // 1) Exact event triggers.
            if (
                block != null
                && s_EventBlocks.Contains(block)
                && s_Events.TryGetValue(block + ":" + index, out List<Trigger> triggers)
            )
            {
                foreach (Trigger trigger in triggers)
                {
                    if (trigger.Key.StartsWith("bg.", StringComparison.Ordinal))
                        DescribeBackground(trigger.Key);
                    else if (!SpokenWithin(trigger.Key, 2))
                        Describe(trigger.Key, trigger.Argument);
                }
                return;
            }

            // 2) Generic rules, only for the story itself (not image/animation blocks).
            RenpyBlock renpyBlock = entry.GetBlock();
            if (renpyBlock == null || !renpyBlock.IsMainLabel)
                return;

            switch (line)
            {
                case RenpyScene scene:
                    OnScene(scene);
                    break;
                case RenpyShow show:
                    OnShow(ParseImageName(show.ShowData, "show"));
                    break;
                case RenpyStandardProxyLib.Expression expression:
                    OnConstants(GetConstantStrings(expression.Expr));
                    break;
                case RenpyOneLinePython oneLine:
                    OnConstants(GetOneLinePythonConstants(oneLine));
                    break;
                default:
                    if (line.GetType().Name == "RenpyDialogueLine")
                        OnDialogueVariant(line);
                    break;
            }
        }

        private static void OnScene(RenpyScene scene)
        {
            // Use the statement text: for "scene bg club_day" the parsed Scene stores
            // Image = "club_day" and treats "bg" as the layer, which would not match the rules.
            string image = ParseImageName(scene.SceneData, "scene");
            if (string.IsNullOrEmpty(image))
                image = scene.scene?.Image;
            if (string.IsNullOrEmpty(image) || !s_SceneRules.TryGetValue(image, out Rule rule))
                return;

            if (rule.Key.StartsWith("bg.", StringComparison.Ordinal))
            {
                DescribeBackground(rule.Key);
            }
            else
            {
                // A full-screen picture replaces the location; announce the location again after it.
                s_LastBackgroundKey = null;
                ApplyRule(rule);
            }
        }

        private static void OnShow(string image)
        {
            if (string.IsNullOrEmpty(image))
                return;

            if (s_ShowRules.TryGetValue(image, out Rule rule))
            {
                if (rule.Key.StartsWith("bg.", StringComparison.Ordinal))
                    DescribeBackground(rule.Key);
                else
                    ApplyRule(rule);
                return;
            }

            foreach (var regexRule in s_RegexRules)
            {
                if (regexRule.Key.IsMatch(image))
                {
                    ApplyRule(regexRule.Value);
                    return;
                }
            }
        }

        private static PropertyInfo s_DialogueTagProperty;
        private static PropertyInfo s_DialogueVariantProperty;

        /// <summary>
        /// A dialogue line like 'y 3y5 "..."' switches the speaker's sprite before speaking;
        /// treat it like "show yuri 3y5". RenpyDialogueLine is internal, hence reflection.
        /// </summary>
        private static void OnDialogueVariant(Line dialogue)
        {
            if (s_DialogueTagProperty == null)
            {
                Type type = dialogue.GetType();
                s_DialogueTagProperty = type.GetProperty("Tag");
                s_DialogueVariantProperty = type.GetProperty("Variant");
            }

            string tag = s_DialogueTagProperty?.GetValue(dialogue, null) as string;
            string variant = s_DialogueVariantProperty?.GetValue(dialogue, null) as string;
            if (string.IsNullOrEmpty(variant) || string.IsNullOrEmpty(tag))
                return;
            if (s_TagToSprite.TryGetValue(tag, out string sprite))
                OnShow(sprite + " " + variant);
        }

        private static void OnConstants(IList<string> constants)
        {
            if (constants == null)
                return;
            foreach (string constant in constants)
            {
                if (constant != null && s_ExprRules.TryGetValue(constant, out Rule rule))
                {
                    ApplyRule(rule);
                    return;
                }
            }
        }

        private static void TrackDialogueStyle(RenpyOneLinePython python)
        {
            IList<string> constants = GetOneLinePythonConstants(python);
            if (constants == null || !constants.Contains("say_dialogue"))
                return;

            if (constants.Contains("edited"))
                EditedStyleActive = true;
            else if (constants.Contains("normal") || constants.Contains("default_monika"))
                EditedStyleActive = false;
        }

        private static void DescribeBackground(string key)
        {
            if (key == s_LastBackgroundKey)
                return;
            s_LastBackgroundKey = key;
            Describe(key);
        }

        private static void ApplyRule(Rule rule)
        {
            if (SpokenWithin(rule.Key, rule.CooldownSeconds))
                return;
            Describe(rule.Key);
        }

        private static bool SpokenWithin(string key, double seconds)
        {
            return s_LastSpoken.TryGetValue(key, out DateTime last)
                && (DateTime.UtcNow - last).TotalSeconds < seconds;
        }

        /// <summary>
        /// "show yuri 3y5 at t11 zorder 2" -> "yuri 3y5"; "scene bg club_day" -> "bg club_day".
        /// </summary>
        public static string ParseImageName(string statement, string verb)
        {
            if (string.IsNullOrEmpty(statement))
                return null;

            string[] tokens = statement.Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );
            int start = tokens.Length > 0 && tokens[0] == verb ? 1 : 0;
            var parts = new List<string>();
            for (int i = start; i < tokens.Length; i++)
            {
                if (Array.IndexOf(s_ShowKeywords, tokens[i]) >= 0)
                    break;
                parts.Add(tokens[i]);
            }
            return parts.Count == 0 ? null : string.Join(" ", parts.ToArray());
        }

        // ------------------------------------------------------------------ compiled expressions

        private static FieldInfo s_OneLinePythonField;
        private static readonly Dictionary<Type, FieldInfo> s_ConstantStringFields =
            new Dictionary<Type, FieldInfo>();

        private static IList<string> GetOneLinePythonConstants(RenpyOneLinePython line)
        {
            if (s_OneLinePythonField == null)
            {
                s_OneLinePythonField = typeof(RenpyOneLinePython).GetField(
                    "m_OneLinePython",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
            }
            var oneLine = s_OneLinePythonField?.GetValue(line) as OneLinePython;
            return oneLine == null ? null : GetConstantStrings(oneLine.compiledExpression);
        }

        /// <summary>Reads CompiledExpression.constantStrings (type lives in another assembly).</summary>
        private static IList<string> GetConstantStrings(object compiledExpression)
        {
            if (compiledExpression == null)
                return null;

            Type type = compiledExpression.GetType();
            if (!s_ConstantStringFields.TryGetValue(type, out FieldInfo field))
            {
                field = type.GetField(
                    "constantStrings",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );
                s_ConstantStringFields[type] = field;
            }

            object value = field?.GetValue(compiledExpression);
            if (value is IList<string> list)
                return list;
            if (value is IEnumerable enumerable)
            {
                var result = new List<string>();
                foreach (object item in enumerable)
                    result.Add(item as string);
                return result;
            }
            return null;
        }

        // ------------------------------------------------------------------ data loading

        private static void EnsureLoaded()
        {
            if (s_Events != null)
                return;

            s_Events = new Dictionary<string, List<Trigger>>(StringComparer.Ordinal);
            s_SceneRules = new Dictionary<string, Rule>(StringComparer.Ordinal);
            s_ShowRules = new Dictionary<string, Rule>(StringComparer.Ordinal);
            s_RegexRules = new List<KeyValuePair<Regex, Rule>>();
            s_ExprRules = new Dictionary<string, Rule>(StringComparer.Ordinal);

            LoadEvents(Loc.ReadResource("SceneEvents.txt"));
            LoadRules(Loc.ReadResource("SceneRules.txt"));

            ScreenReaderMod.Logger?.Msg(
                $"Scene descriptions loaded: {s_Events.Count} events, "
                    + $"{s_SceneRules.Count + s_ShowRules.Count + s_RegexRules.Count + s_ExprRules.Count} rules"
            );
        }

        private static void LoadEvents(string text)
        {
            if (text == null)
            {
                ScreenReaderMod.Logger?.Error("SceneEvents.txt resource missing");
                return;
            }

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                string[] parts = line.Split('|');
                if (parts.Length < 3)
                    continue;

                string location = parts[0].Trim();
                var triggers = new List<Trigger>();
                foreach (string keyPart in parts[parts.Length - 1].Split(','))
                {
                    string spec = keyPart.Trim();
                    if (spec.Length == 0)
                        continue;
                    int colon = spec.IndexOf(':');
                    triggers.Add(
                        colon < 0
                            ? new Trigger { Key = spec }
                            : new Trigger
                            {
                                Key = spec.Substring(0, colon),
                                Argument = spec.Substring(colon + 1),
                            }
                    );
                }
                s_Events[location] = triggers;
                int colonIndex = location.LastIndexOf(':');
                if (colonIndex > 0)
                    s_EventBlocks.Add(location.Substring(0, colonIndex));
            }
        }

        private static void LoadRules(string text)
        {
            if (text == null)
            {
                ScreenReaderMod.Logger?.Error("SceneRules.txt resource missing");
                return;
            }

            foreach (var pair in Loc.ParseKeyValueLines(text))
            {
                string selector = pair.Key;
                string[] valueParts = pair.Value.Split('|');
                var rule = new Rule
                {
                    Key = valueParts[0].Trim(),
                    CooldownSeconds =
                        valueParts.Length > 1
                        && double.TryParse(
                            valueParts[1].Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double cooldown
                        )
                            ? cooldown
                            : DefaultCooldownSeconds,
                };

                int colon = selector.IndexOf(':');
                if (colon <= 0)
                    continue;
                string kind = selector.Substring(0, colon);
                string name = selector.Substring(colon + 1).Trim();

                switch (kind)
                {
                    case "scene":
                        s_SceneRules[name] = rule;
                        break;
                    case "show":
                        s_ShowRules[name] = rule;
                        break;
                    case "regex":
                        s_RegexRules.Add(
                            new KeyValuePair<Regex, Rule>(new Regex(name, RegexOptions.Compiled), rule)
                        );
                        break;
                    case "expr":
                        s_ExprRules[name] = rule;
                        break;
                }
            }
        }
    }
}
