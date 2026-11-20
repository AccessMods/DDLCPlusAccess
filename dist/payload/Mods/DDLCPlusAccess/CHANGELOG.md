# Changelog

## 2.3.1

- Fixed the mod staying silent on some computers. The log showed "Prism failed to start:
  System.DllNotFoundException: prism" even though prism.dll was installed.

## 2.3.0

- The gallery is now fully translated: every picture, wallpaper, sketch, background and promo
  description is read in the game's language, and gallery poems read the game's own
  translation of the poem. Images are announced by a readable name (for example "Wallpaper:
  Monika") instead of internal file names like "warning2".
- Gallery captions and unlock conditions are read the way the game shows them, in your language.
- All gallery messages (views, sections, positions, wallpaper actions) are translated.

## 2.2.0

- Glitched and unusual poems now come with a description of how they look before their text:
  Sayori's "Get out of my head" poem, Natsuki's encoded poem, Yuri's "Wheel" and her final
  scrawled poem, both versions of Monika's "Save Me", and the stained papers in the side stories.
- Special poems are described in every language, quoting the game's own translation of their
  text. Before, the descriptions were English only, and in some languages they were missing.
  The gallery's special poems use the same descriptions.
- Pictures opened from the in-game Files app are described (hxppy thxughts.png) instead of only
  saying "not a text file". The file viewer's messages are now translated too.

## 2.1.0

- Scene descriptions: the mod now describes what happens on screen as the story plays. It covers
  locations, every story picture (CG), and every visual-only event, including the horror and
  glitch scenes of Acts 1 to 4. Rare and secret events are covered too: Natsuki's ghost, the ghost
  menu, the early splash screen, the Act 3 jump scare, the random Act 2 clubroom, the eye
  flashes, the crash screens and Monika's console commands. DDLC Plus side stories and credits
  are described as well.
- Glitches explained: random glitch symbols in dialogue and character names are read as
  "(glitched text)" or "(glitched name)". Act 2's garbled "New Game" button, the glitched poem
  words and the Act 3 "Monika" words are described. Lines shown in Monika's distorted "edited"
  font start with "Distorted text:".
- The main menu artwork is described for each act, including the ghost menu.
- New key: D repeats the last scene description.
- Everything the mod says now follows the game's language: English, Spanish (Spain and Latin
  America), French, German, Italian, Brazilian Portuguese, Russian, Japanese, Korean, and
  Simplified and Traditional Chinese.
- Speech now goes through Prism (NVDA, JAWS, Narrator and others, or the Windows voices when no
  screen reader runs) instead of UniversalSpeech, which was missing from installs and left the
  mod silent.
- New installer: DDLCPlusAccessibilityInstaller.exe installs MelonLoader and the mod, updates
  them, and removes the old DDLCScreenReaderMod.dll so no line is read twice.
