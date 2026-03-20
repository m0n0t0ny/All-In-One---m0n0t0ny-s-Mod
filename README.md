# All In One — m0n0t0ny's Mod

A quality-of-life mod for **Escape from Duckov** with 13 independent features, all toggleable from the **F9** settings panel.

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

---

## Features

| Feature | Default | Key |
|---|---|---|
| Item sell value on hover (single / stack / combined) | ON | — |
| Enemy names above health bar | ON | — |
| Modifier+click to transfer items (container ↔ backpack) | ON | Shift or Alt (configurable) |
| Auto-close container on WASD / Shift / Space / damage | OFF | — |
| Sleep preset buttons (rain / Storm I / Storm II / post-storm / 4 custom times) | ON | — |
| Recorded items badge (✓ on known keys & blueprints) | ON | — |
| FPS counter (top-right) | OFF | — |
| Skip melee slot on scroll wheel | ON | — |
| Auto-unload enemy gun on kill | ON | — |
| Lootbox highlight — gold outline on loot containers | ON | — |
| Kill feed — killer → victim, [HS] on headshots | ON | — |
| Quest favorites — pin a quest to the top of the list | ON | N |
| Hide controls hint button | ON | — |

All settings are persisted via PlayerPrefs and configurable from the **F9** panel in-game.

---

## Installation

### Steam (recommended)

Subscribe on the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781) and enable the mod from the **Mods** menu in the main menu.

### Manual (Epic Games and other platforms)

1. Download `AllInOneMod_m0n0t0ny_vX.Y.zip` from the [latest release](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)
2. Extract the `AllInOneMod_m0n0t0ny` folder into:
   ```
   <game folder>/Duckov_Data/Mods/
   ```
   Create the `Mods` folder if it doesn't exist.
3. Launch the game → **Mods** in the main menu → enable the mod

---

## Building from source

Requirements: .NET SDK, Escape from Duckov installed.

1. Open `duckov_modding/m0n0t0nysMod.sln` in Visual Studio or Rider
2. Set `DuckovPath` in `m0n0t0nysMod.csproj` to your game installation folder
3. Build in Release configuration — the output DLL is in `bin/Release/netstandard2.1/`

---

## Changelog

See [Releases](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases) for the full version history.
