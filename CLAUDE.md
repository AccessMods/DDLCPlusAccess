# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Development Commands

### Build Commands

- **Build the project**: `dotnet build` or `MSBuild DDLCPlusAccess.csproj`
- **Clean build**: `dotnet clean && dotnet build`
- **Release build**: `dotnet build --configuration Release`

### Testing Installation

- Post-build automatically copies `DDLCPlusAccess.dll` and `UnityAccessibilityLib.dll` to the DDLC Plus `Mods` folder
- Launch DDLC Plus with MelonLoader to test

## Project Architecture

### Core Components

**Main.cs** - Main mod entry point (MelonMod)

- Initializes UnityAccessibilityLib (`SpeechManager`, `TextCleaner`, `AccessibilityLog`)
- Registers Ren'Py/TMP-specific text cleaning patterns with `TextCleaner.AddRegexReplacement()`
- Handles scene loading events
- Implements hotkeys:
  - **R key**: Repeat last dialogue (`SpeechManager.RepeatLast()`)
  - **D key**: Repeat last scene description (`SceneDescriber.RepeatLastDescription()`)
  - **C key**: Announce data collection percentage (in Settings app)
  - **P key**: Announce jukebox position (in Jukebox app)
  - **Up/Down arrows**: Navigate history entries (when history screen has focus) or lines in file content viewer

**ClipboardUtils.cs** - Thin wrapper around UnityAccessibilityLib's SpeechManager

- `OutputGameText(speaker, text, textType)` → `SpeechManager.Output()` or `SpeechManager.Announce()`
- `OutputPoemText(text)` → `SpeechManager.Announce()`
- `RepeatCurrentDialogue()` → `SpeechManager.RepeatLast()`
- Maintains API compatibility with all patch files

**GameTextType.cs** - Text type constants and mapping

- Maps legacy `TextType` enum to library's `TextType` constants
- Defines custom types: PoetryGame, FileBrowser, Poem, Settings, Mail, Jukebox
- Provides `GetTextTypeNames()` for logging and `ShouldStoreForRepeat()` predicate

**MelonLoggerAdapter.cs** - Bridges MelonLoader logging to UnityAccessibilityLib

**TextHelper.cs** - Supplementary text processing utilities

- `CleanText()` - Wraps `TextCleaner.Clean()`
- `IsNarrativeText()` - Determines if text is narrative (no speaker)

**SpeakerMapping.cs** - Speaker name resolution

- `GetLocalizedSpeakerName(tag, character)` - Gets localized speaker name using game's localization system
- Replicates game's exact logic from `RenpyWindowManager.Say` for accurate speaker names
- Handles player name (MC) via `GetPlayerName()`
- `ShouldFilterText()` - Filters developer commentary and technical strings

**SceneDescriptions/SceneDescriber.cs** - Audio descriptions of on-screen events

- Harmony postfix on `RenpyCallstackEntry.Next` sees every script line the game runs (block name + line index)
- `SceneEvents.txt` (embedded): exact `block:line | expected content | key[:arg]` triggers for one-off events
- `SceneRules.txt` (embedded): generic rules by `scene:`/`show:`/`regex:`/`expr:` name (locations, CGs, special sprites, screen tears); per-key cooldowns; locations only announced when they change
- Tracks the "edited" dialogue style (Monika's distorted font) so `DialoguePatches` can mark those lines
- Output type `GameTextType.Description` (107)

**Localization/Loc.cs** - Localized mod strings

- `Loc.Get(key, args)`; language from `Renpy.GetCurrentLanguagePrefix()` (en-us, spa, spa-m9, fr-fr, de-de, it-it, pt-br, pyc, ko, ja, zh-cn, zh-tw)
- One embedded `Localization/<lang>.txt` per language (`key = value`); `en-us.txt` is the master, missing keys fall back to English
- New user-facing strings must be added to all 12 files

**Utilities/PoemVisualDescriptions.cs** - Recognizes glitched/unusual poems (by raw author/title/text) and stained papers (`m_Paper`); `PoemPatches` speaks the `poem.desc_*` / `poem.paper_*` description before the poem

**Utilities/SpecialPoemDescriptions.cs** - Maps special poem images in any language variant (`poem_special1_spa`, `_th`, `_original`, `gallery_special*`) to localized `poem.special*` keys; their text quotes the game's own translation

**Utilities/GlitchText.cs** - Detects the game's random glitch text (Latin Extended-A + rare symbols) and replaces it with a localized marker

**DescriptionManager.cs** - Centralized image description system

- Manages descriptions for gallery images (CGs, Wallpapers, Poems, Backgrounds, etc.)
- Provides lookup methods by image name or section

### Harmony Patching Architecture

The mod uses Harmony to patch game methods. All patches output text via `ClipboardUtils.OutputGameText()`:

| Patch File              | Purpose                                               |
| ----------------------- | ----------------------------------------------------- |
| `DialoguePatches.cs`    | Character dialogue via `RenpyWindowManager.Say` patch |
| `HistoryPatches.cs`     | History screen navigation with Up/Down arrow keys     |
| `MenuPatches.cs`        | Menu navigation, choices, settings                    |
| `LauncherPatches.cs`    | DDLC Plus launcher accessibility                      |
| `FileBrowserPatches.cs` | File browser navigation                               |
| `FileContentPatches.cs` | File content viewing                                  |
| `PoetryPatches.cs`      | Poetry minigame                                       |
| `PoemPatches.cs`        | Poem reading                                          |
| `SideStoriesPatches.cs` | Side stories navigation                               |
| `GalleryPatches.cs`     | Gallery navigation and descriptions                   |
| `ImagePatches.cs`       | Background/CG announcements                           |
| `JukeboxPatches.cs`     | Music player                                          |
| `MailPatches.cs`        | Email application                                     |
| `SelectorPatches.cs`    | Generic selector UI                                   |
| `TextPatches.cs`        | Fallback text capture                                 |

### Text Output Types

Defined in `GameTextType.cs` (legacy enum in same file for patch compatibility):

- **Dialogue** (0): Character speech with speaker names
- **Narrator** (1): Descriptive text without speakers
- **Menu** (2): Menu navigation
- **MenuChoice** (3): Game choices
- **SystemMessage** (4): System notifications
- **PoetryGame** (101): Poetry minigame
- **FileBrowser** (102): File browser
- **Poem** (103): Poem content
- **Settings** (104): Settings values
- **Mail** (105): Email content
- **Jukebox** (106): Music player info

### Text Cleaning

Text cleaning is handled by UnityAccessibilityLib's `TextCleaner` with custom patterns registered in `ScreenReaderMod.RegisterTextCleanerPatterns()`:

- Ren'Py tags: `{w}`, `{nw}`, `{clear}`, etc. (pattern: `\{[^}]*\}`)
- TMP square bracket tags: `[color=...]`, `[size=...]`, `[b]`, `[i]`, etc.
- Special replacements: keyboard sprite → "Press Enter to Apply"

## Dependencies

- **.NET Framework 4.7.2** target
- **UnityAccessibilityLib** (NuGet) - Speech output, text cleaning, duplicate detection
- **MelonLoader** - Mod loading framework
- **Harmony** - Runtime method patching
- **Unity Engine** - Game engine APIs
- **DDLC/RenpyParser** - Game-specific assemblies
- Decompile `DDLC.dll` / `Assembly-CSharp.dll` with `ilspycmd` for reference (not kept in the repo)
- Build finds the game at `C:\Program Files (x86)\Steam\...` or `S:\steam\...`; override with `-p:GamePath=...`

## Development Guidelines

### Adding New Text Sources

1. Identify the game method that displays the text
2. Create Harmony patch in relevant Patches file
3. Call `ClipboardUtils.OutputGameText(speaker, text, TextType.X)`
4. Library handles text cleaning and duplicate detection automatically

### Adding Scene/Event Descriptions

1. Decode the script with `tools/` (see `tools/README.md`) and locate the line in `script.txt`
2. Add the description key to `Localization/en-us.txt` and all other language files
3. Reference it from `SceneDescriptions/SceneEvents.txt` or `SceneRules.txt`
4. Run `python tools/validate_scene_events.py tools . <work>/script.txt`

### Adding New Image Descriptions

1. Identify the image asset name in the game
2. Add description to appropriate file in `ImageDescriptions/` folder
3. Dictionary key = asset name, value = description text

### Debugging

- Use MelonLoader console to monitor patch application and speech output
- Library logs all speech output with text type names
- All logging enabled by default

## Accessibility Focus

This mod improves game accessibility for blind and visually impaired players. All changes should:

- Preserve screen reader compatibility
- Maintain clear, descriptive text output
- Avoid excessive or duplicate announcements
