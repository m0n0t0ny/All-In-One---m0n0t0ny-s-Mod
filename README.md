# ALL IN ONE - m0n0t0ny's mod

English | [Italiano](README_IT.md) | [Français](README_FR.md) | [Deutsch](README_DE.md) | [中文简体](README_ZH_CN.md) | [中文繁體](README_ZH_TW.md) | [日本語](README_JA.md) | [한국어](README_KO.md) | [Português](README_PT_BR.md) | [Русский](README_RU.md) | [Español](README_ES.md)

All-in-one quality of life mod for **Escape from Duckov**. 20 independent features, all configurable from the native **Settings** menu.

[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Subscribe-1b2838?logo=steam)](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
[![Latest Release](https://img.shields.io/github/v/release/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod)](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)

![Preview](AllInOneMod_m0n0t0ny/preview.png)

---

## Features

All settings are persisted and configurable from the **ALL IN ONE** tab in the game's Settings menu - accessible both from the main menu and the in-game pause menu.

![Options menu](assets/f9-menu.png)

---

### 🎒 Looting

#### Show item value on hover
Shows the sell price of any item at any time, not just in shops. Choose between combined, single, stack, or off.

![Item sell value](assets/item-sell-value.png)

#### Inventory count on hover
Shows how many of the hovered item you are carrying and how many are in your stash. Toggleable from settings (ON by default).

#### Quick item transfer
Alt+click or Shift+click to instantly move items between an open container and your backpack, and vice versa.

#### Auto-unload gun on kill
When you loot a killed enemy, their weapon is automatically unloaded - ammo goes directly into the stash as a lootable stack, ready to grab.

#### Badge on recorded keys and Blueprints
A green checkmark on keys and blueprints you have already recorded, so you know at a glance what to keep and what to sell.

![Recorded items badge](assets/recorded-items-badge.png)

#### Lootbox highlight
Colored outline on loot containers in the world so you never miss one. Three modes: All / Only unsearched / Off. Border color follows item rarity (white for empty containers).

![Lootbox highlight](assets/lootbox-highlight.png)

#### Item rarity display
Colored border on inventory slots based on item sell value. Six tiers from white (low value) to red (high value). Toggleable from settings.

![Item rarity display](assets/item-rarity-display.png)

#### Item name label
Item names on inventory slots are centered and shown without background label.

---

### ⚔️ Combat

#### Show enemy name
Displays the enemy name above their health bar.

![Enemy names](assets/enemy-names.png)

#### Kill feed
Shows kills in the top-right corner during raids - killer, victim, and [HS] tag on headshots.

#### Boss map markers
Real-time markers on the fullscreen map for each boss, color-coded (red=alive, grey=dead). A boss list overlay appears when the map is open. Toggleable from settings (ON by default).

#### Show hidden enemy health bars
Forces health bars visible on enemies whose bar is hidden by default (e.g. the ??? boss). Toggleable from settings (ON by default).

#### Skip melee on scroll
Scroll wheel skips the melee slot when cycling weapons. Melee can still be equipped via V.

---

### 🌙 Survival

#### Wake-up presets
Wake-up preset buttons on the sleep screen: 4 custom configurable times, plus rain, Storm I, Storm II, and Storm end.

![Sleep presets](assets/sleep-presets.png)

#### Auto-close container
Automatically closes an open container when pressing WASD, Shift, Space, or on taking damage. Each trigger is independently toggleable.

---

### 🖥️ HUD

#### FPS counter
Displays current FPS in the top-right corner (OFF by default).

#### Hide controls hint
Hides the native Controls [O] button and its submenu to reduce HUD clutter.

![Hide controls hint](assets/hide-controls-hint.png)

#### Hide HUD on ADS
Hides the HUD while holding right-click for a cleaner, more immersive aiming experience. Three modes: Hide all / Show only ammo / Off. Health bars and crosshair always remain visible.

![Hide HUD on ADS](assets/hide-hud-on-ads.png)

#### Camera view
Three-mode setting: Off / Default / Top-down. The selected view is applied immediately and restored automatically on scene load.

---

### ⭐ Quests

#### Quest favorites (N key)
Press N on a selected quest to pin it to the top of the list. Pinned quests are always visible regardless of filters.

![Quest favorites](assets/quest-favorites.png)

---

## Installation

### Steam (recommended)

1. Subscribe on the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3685814781)
2. Launch the game -> **Mods** in the main menu -> enable the mod

The mod updates automatically whenever a new version is published.

### Manual

1. Download the latest zip from the [Releases page](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases/latest)
2. Extract the `AllInOneMod_m0n0t0ny` folder into the `Mods` folder of your game installation (create it if it doesn't exist):

   | Platform             | Path                                                                                 |
   | -------------------- | ------------------------------------------------------------------------------------ |
   | Steam (Windows)      | `C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | Epic Games (Windows) | `C:\Program Files\Epic Games\EscapeFromDuckov\Duckov_Data\Mods\`                     |
   | Steam (Linux)        | `~/.steam/steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/`               |

3. Launch the game -> **Mods** in the main menu -> enable the mod

To update manually, replace the `AllInOneMod_m0n0t0ny` folder with the new version.

---

## Changelog

See [Releases](https://github.com/m0n0t0ny/All-In-One---m0n0t0ny-s-Mod/releases) for the full version history.
