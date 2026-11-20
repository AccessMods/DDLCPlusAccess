# Script extraction tools

DDLC Plus ships its Ren'Py script pre-compiled inside Unity assets. These tools turn it back
into readable text so scene descriptions can be written and their trigger lines verified.
They are not part of the mod build.

## 1. Extract the raw data

```
pip install UnityPy
python tools/extract_assets.py "<game>/Doki Doki Literature Club Plus_Data" work
```

## 2. Decode it to JSON

The assets have no Unity type tree, so `unser` derives the serialized layout from the
game's own `DDLC.dll` with Mono.Cecil (shipped with MelonLoader):

```
dotnet build tools/unser -c Release -o work/unser -p:GamePath="<game>"
work/unser/unser "<game>/Doki Doki Literature Club Plus_Data/Managed" DDLC.dll Blocks       work/blocks.bin           work/blocks.json
work/unser/unser "<game>/Doki Doki Literature Club Plus_Data/Managed" DDLC.dll Lines        work/Lines_en-us.bin      work/lines_en-us.json
work/unser/unser "<game>/Doki Doki Literature Club Plus_Data/Managed" DDLC.dll AudioDefines work/AudioDefines.bin     work/audio.json
```

Each run prints `consumed N of N`; if the two numbers differ the layout is wrong.

## 3. Render the script

```
python tools/dump.py work        # writes work/script.txt
```

`script.txt` lists every block (label) with its numbered lines: dialogue with English text,
`show`/`scene`/`hide`, python, audio, jumps and menus. The line numbers are the indices that
`SceneDescriptions/SceneEvents.txt` refers to.

## 4. Validate the scene events

```
python tools/validate_scene_events.py tools . work/script.txt
```

Checks that every `block:line` in `SceneEvents.txt` still contains its expected text and that
every key used in `SceneEvents.txt` / `SceneRules.txt` exists in `Localization/en-us.txt`.
