# Blueprinter

A BepInEx mod for Nuclear Option ([https://store.steampowered.com/app/2168680/Nuclear\_Option/](https://store.steampowered.com/app/2168680/Nuclear_Option/)) to load custom content mods.

Blueprinter loads `.nobp` mods built with Blueprinter Editor.

Blueprinter Unity Editor Project to make the mods (https://github.com/nikkorap/NOBlueprinter-Editor)

## Installation

Blueprinter requires `BepInEx 5 for Windows x64`.

1. Install BepInEx in the Nuclear Option game folder.
2. Download the latest Blueprinter `.dll` from the `Releases` page and place it under `BepInEx/plugins`.
3. Place `.nobp` mod files anywhere under `BepInEx/plugins`, including subfolders.

Blueprinter scans `BepInEx/plugins` and its subfolders on game start.

## Configuration

Blueprinter has two optional settings.

- `FastLoad`: Loads every `.nobp` file directly without checking for duplicate or conflicting mod versions. Only enable this when you don't have duplicate versions of any mod installed.
- `SkipAdditionalAssets`: Skips loading additional game assets used by some mods, for a small further speedup. Mods that depend on these assets may not work correctly with this enabled.
