# DDLCPlusAccess

A comprehensive accessibility mod for **Doki Doki Literature Club Plus** that provides screen reader support for blind and visually impaired players. This MelonLoader mod intercepts game text and outputs it directly to your screen reader using speech synthesis.

## Features

### Complete Game Coverage

- **Character Dialogue**: All character speech with localized speaker names
- **History Screen**: Navigate through dialogue history with Up/Down arrows
- **Menu Navigation**: Main menu, preferences, save/load screens
- **Poetry Minigame**: Word selection feedback and character reactions
- **File Browser**: Directory navigation with item counts and file operations
- **File Content Viewer**: Line-by-line navigation of text files with Up/Down arrows
- **Side Stories**: Accessible launcher navigation and story selection
- **Poems**: Formatted poem content including special poems with visual descriptions
- **Gallery**: Full descriptions for all CGs, wallpapers, poems, backgrounds, sketches, promotional art, and secret images
- **Mail App**: Email content and navigation
- **Jukebox**: Music player with track information and position announcements
- **System Messages**: Game notifications, boot sequences, and status updates
- **Scene Descriptions**: Spoken audio descriptions of what happens on screen: locations, story pictures (CGs), and every visual-only event, including the horror and glitch scenes of Acts 1-4, rare/secret events (Natsuki's ghost, the ghost menu, the early splash, the Act 3 jump scare) and DDLC Plus side stories and credits
- **Glitches Explained**: Glitched text, names, menu buttons (such as Act 2's garbled "New Game") and poem-game words are described instead of read out as random symbols; lines shown in the distorted "edited" font are marked
- **12 Languages**: Everything the mod says follows the game's language: English, Spanish (Spain and Latin America), French, German, Italian, Brazilian Portuguese, Russian, Japanese, Korean, and Simplified and Traditional Chinese

### Accessibility Features

- **Direct Speech Output**: Text spoken immediately via UnityAccessibilityLib
- **Speaker Identification**: Character names always included in dialogue
- **Duplicate Prevention**: Smart filtering to avoid repeated announcements
- **Real-time Processing**: Text appears immediately as displayed in game
- **Image Descriptions**: Comprehensive descriptions for all gallery content
- **No Configuration Required**: Works out of the box with optimal settings

### Keyboard Shortcuts

| Key         | Function                                                |
| ----------- | ------------------------------------------------------- |
| **R**       | Repeat last dialogue                                    |
| **D**       | Repeat last scene description                           |
| **C**       | Announce data collection percentage (Settings app only) |
| **P**       | Announce current position in playlist (Jukebox only)    |
| **Up/Down** | Navigate history entries or file content lines          |

## Installation

### Prerequisites

- **Doki Doki Literature Club Plus** (Steam version)
- Windows operating system

### Easy Installation (Recommended)

1. Download `DDLCPlusAccessibilityInstaller.exe` from the [latest release](https://github.com/gomods1/ddlcplus/releases/latest)
2. Run it. It finds your game through Steam (or lets you browse to it), shows the installed and latest versions, and installs, updates or uninstalls the mod
3. It installs MelonLoader 0.7.1 and the mod, and removes the mod's old file `Mods\DDLCScreenReaderMod.dll` if present (otherwise every line would be read twice)

The installer is screen reader accessible. Run it with `--cli` for a terminal flow.

### Manual Installation

1. Download `DDLCPlusAccess-vX.Y.Z.zip` from the [latest release](https://github.com/gomods1/ddlcplus/releases/latest)
2. Extract **everything** into the DDLC Plus folder (the one with `Doki Doki Literature Club Plus.exe`), keeping the folders. You get:
   - `version.dll` and `MelonLoader\` (the mod loader)
   - `UnityAccessibilityLib.dll`
   - `Mods\DDLCPlusAccess.dll` (the mod)
   - `Mods\DDLCPlusAccess\prism.dll` (speech output; **without it the mod is silent**), plus licences and this README
3. Delete `Mods\DDLCScreenReaderMod.dll` if you have it (the mod's old name)
4. Launch DDLC Plus from Steam

### Verification

- Your screen reader says that DDLC Plus Access is loaded, in the game's language
- `MelonLoader\Latest.log` names the speech backend, e.g. `Prism 0.18.3 loaded ... speaking through NVDA`
- Press **R** to repeat the last dialogue and **D** to repeat the last scene description

## Usage

### Basic Operation

The mod works automatically once installed. All game text is spoken directly to your screen reader.

### History Navigation

When viewing dialogue history (accessible via the game's history button), use Up/Down arrow keys to navigate through previous dialogue entries.

### File Content Navigation

When viewing text files in the file browser, use Up/Down arrow keys to navigate line by line through the file content.

## Development

### Building the Mod

```bash
# Standard build
dotnet build

# Clean build
dotnet clean && dotnet build

# Release build
dotnet build --configuration Release
```

The build automatically copies the mod files to the game's Mods folder.

### Dependencies

- .NET Framework 4.7.2
- UnityAccessibilityLib (NuGet package)
- MelonLoader framework
- Harmony patching library
- Unity Engine APIs
- DDLC/RenpyParser assemblies

### Project Structure

- `Main.cs` - Main mod entry point and initialization
- `ClipboardUtils.cs` - Speech output wrapper for UnityAccessibilityLib
- `DescriptionManager.cs` - Centralized image description system
- `GameTextType.cs` - Text type definitions and mapping
- `TextHelper.cs` - Text cleaning and formatting utilities
- `SpeakerMapping.cs` - Character name resolution
- `Patches/` - Harmony patches for different game systems
- `ImageDescriptions/` - Gallery image description data
- `SceneDescriptions/` - Scene/event description engine (`SceneDescriber.cs`) and its trigger data (`SceneEvents.txt`, `SceneRules.txt`)
- `Localization/` - Localized mod strings, one file per game language (`en-us.txt` is the master)
- `tools/` - Offline tools to decode the game's compiled script and validate scene triggers (see `tools/README.md`)

### Harmony Patches

| Patch File              | Purpose                               |
| ----------------------- | ------------------------------------- |
| `DialoguePatches.cs`    | Character dialogue and narrative text |
| `HistoryPatches.cs`     | History screen navigation             |
| `MenuPatches.cs`        | Menu navigation and UI interactions   |
| `LauncherPatches.cs`    | DDLC Plus launcher accessibility      |
| `FileBrowserPatches.cs` | File system navigation                |
| `FileContentPatches.cs` | File content viewing                  |
| `PoetryPatches.cs`      | Poetry minigame interactions          |
| `PoemPatches.cs`        | Poem reading and formatting           |
| `SideStoriesPatches.cs` | Side story navigation                 |
| `GalleryPatches.cs`     | Gallery navigation and descriptions   |
| `ImagePatches.cs`       | Background/CG announcements           |
| `JukeboxPatches.cs`     | Music player accessibility            |
| `MailPatches.cs`        | Email application                     |
| `SelectorPatches.cs`    | Generic selector UI                   |
| `TextPatches.cs`        | Fallback text capture                 |

### Adding Scene Descriptions

1. Decode the game script with the tools in `tools/` and find the line in `script.txt`.
2. Add a description key to `Localization/en-us.txt` and every other language file.
3. Trigger it from `SceneDescriptions/SceneEvents.txt` (exact `block:line`) or `SceneRules.txt` (by image or effect name).
4. Run `tools/validate_scene_events.py` to check line indices and translations.

## Compatibility

### Supported Versions

- **DDLC Plus**: All current versions
- **MelonLoader**: Compatible with standard MelonLoader installations
- **Screen Readers**: Works with NVDA, JAWS, Narrator and other Windows screen readers through [Prism](https://github.com/ethindp/prism), which falls back to the Windows voices when no screen reader runs

### Known Limitations

- Windows-only (uses Windows speech APIs)
- Requires MelonLoader framework

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

For issues or questions:

- Check MelonLoader console for error messages
- Verify all DLL files are in the correct locations
- Ensure DDLC Plus and MelonLoader are properly installed
- Report issues at the project repository
