# ModernBar

A segmented info bar for ExileAPI. No pipes.

> **Note:** ModernBar replaces the default MiscInformation bar. Turn off
> MiscInformation in the HUD settings (F12) to avoid doubled info.

## Features

![ModernBar with all segments enabled](screenshots/bar-full.png)

| Level, area, DPS | Time and XP rate |
| --- | --- |
| ![Level, area and DPS](screenshots/seg-level-dps.png) | ![Time and XP rate](screenshots/seg-time-xp.png) |
| **Gold and ping** | **Preload colors** |
| ![Gold and ping](screenshots/seg-gold-ping.png) | ![Preload colors](screenshots/preload-colors.png) |

Quiet moments show DPS near zero. With no damage recorded it shows zeros:

![Quiet DPS under peak](screenshots/bar-dps-quiet.png)

Preload tracking uses per-pattern colors with a regex tester, and every
segment has editable themes. The Preloads tab edits the pattern file:
groups with counts, per-pattern toggles and colors, duplicate
display-name warnings.

![Preload pattern editor](screenshots/preload-editor.png)

## Settings

The Info Bar tab controls which segments appear and where the bar sits. Preloads manages the pattern file and
shows duplicate display names in matching colors. Themes holds the palette
and alpha values.

## Installation

1. Copy `ModernBar/` into `Plugins/Source/`.
2. Start the HUD. It compiles source plugins at startup.
3. Enable the plugin in the HUD menu.

## Version

v1.0. See `CHANGELOG.md`.
