# Claude instructions — m0n0t0ny's mod (Escape from Duckov)

## After EVERY code change

1. **Build** (specify the csproj to avoid ambiguity):
   ```
   cd "c:/Users/antob/Desktop/Escape From Duckov/duckov_modding/m0n0t0nysMod" && dotnet build m0n0t0nysMod.csproj -c Release
   ```

2. **Install** the DLL (game must be closed):
   ```
   powershell -Command "Copy-Item 'c:\Users\antob\Desktop\Escape From Duckov\duckov_modding\m0n0t0nysMod\bin\Release\netstandard2.1\AllInOneMod_m0n0t0ny.dll' -Destination 'C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\AllInOneMod_m0n0t0ny\AllInOneMod_m0n0t0ny.dll' -Force"
   ```

3. **Verify** the install succeeded (check size and timestamp changed):
   ```
   powershell -Command "Get-Item 'C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\AllInOneMod_m0n0t0ny\AllInOneMod_m0n0t0ny.dll' | Select-Object Length, LastWriteTime | Format-List"
   ```
   If the copy fails with "mapped section" error, the game is still open — ask the user to close it first.

## Whenever a feature is added, changed, or removed

4. **Update `info.ini` description** in both locations:
   - `m0n0t0nysMod/ReleaseExample/AllInOneMod_m0n0t0ny/info.ini`
   - `C:/Program Files (x86)/Steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/AllInOneMod_m0n0t0ny/info.ini`
   - Format: spaces escaped with `\ `, features separated by ` | `

5. **Bump the version** in both `info.ini` files, in the `Mod info` table below, **and in `BuildSettingsPanel()` in `ModBehaviour.cs`** (the `"vX.Y"` string in the header `LText` call).
   - Patch bump (X.Y+1): bugfix or UI tweak
   - Minor bump (X+1.0): new feature

6. **Add a changelog entry** at the top of the changelog section below.

7. **Verify the F9 settings menu** reflects the new feature:
   - If there is a new toggle or option, confirm it is visible in `BuildSettingsPanel()` in `ModBehaviour.cs`.
   - If a feature was removed, confirm its toggle/option was also removed from the settings panel.
   - If a default value changed, confirm the matching `PlayerPrefs.GetInt(..., default)` was updated in `Awake()`.

---

## Mod info

| Field | Value |
|---|---|
| Technical name (folder, DLL, `name` in info.ini) | `AllInOneMod_m0n0t0ny` |
| Namespace | `AllInOneMod_m0n0t0ny` |
| Display name | `All In One - m0n0t0ny's Mod` |
| Settings key | F9 |
| Current version | 2.1 |

## Feature list (keep in sync with info.ini description)

- Item sell value on hover: shows sell price at any time, not just in shops (single / stack / combined display)
- Enemy names: displays enemy name above health bar (toggleable)
- Item transfer: Modifier+click (Shift or Alt, configurable) to move items between container and backpack (toggleable)
- Auto-close container: closes on WASD / Shift / Space / damage received (each independently toggleable)
- Sleep presets: wake at 4 custom configurable times, until rain, Storm I, Storm II, post-storm
- Recorded items badge: green badge (✓) on keys and blueprints already recorded (toggleable)
- FPS counter: top-right corner (toggleable, OFF by default)
- Skip melee on scroll: scroll wheel skips the melee slot; melee only equippable via V key (toggleable, ON by default)
- Auto-unload on kill: when killing an enemy, their equipped gun's plugged items (ammo/magazine) are automatically moved to their stash (toggleable, ON by default)
- F9 opens settings menu with all toggles and configurable preset times

---

## Changelog

### v2.1
- Auto-unload on kill: when an enemy dies, their equipped gun's plugged items (ammo/magazine) are automatically moved to their stash; player can loot the bullets directly without manually unloading the weapon (ON by default, toggleable in F9)

### v2.0
- Skip melee on scroll: scroll wheel weapon cycle now skips the melee slot (ON by default); melee can still be equipped via the dedicated V key

### v1.9
- Sleep presets expanded from 2 to 4 configurable custom times (Preset 3 default 08:00, Preset 4 default 12:00)

### v1.8
- Added FPS counter: shows current FPS in the top-right corner of the screen (toggleable in F9 settings, OFF by default)

### v1.7
- Added recorded items badge: green ✓ badge on inventory slots for blueprints and keys already recorded (toggleable in F9 settings)
- Removed temporary debug discovery code

### v1.6
- Added auto-close container feature: closes open container when pressing WASD, Shift, Space, or on taking damage (each toggle independent, all OFF by default)
- Fix: item transfer (container↔backpack) now uses direct inventory manipulation instead of missing LootItem reflection

### v1.5
- Fix: item transfer now uses LateUpdate snapshot to avoid race condition where EventSystem clears hover before Update() runs

### v1.4
- Fix: sleep preset buttons now expand the slider max when target time exceeds 24h (e.g. Storm I far away)

### v1.3
- Item transfer modifier key now configurable (Shift or Alt) via F9 settings
- Added toggle to enable/disable the item transfer feature

### v1.2
- Added Shift+LMB to transfer items between container and backpack (both directions)

### v1.1
- Added enemy name display above health bars (toggleable in F9 settings)

### v1.0
- Initial release
- Item sell value on hover (always active)
- Single / stack / combined display modes
- Sleep preset buttons (6 presets: 2 custom times + rain + Storm I/II + post-storm)
- Sleep presets configurable from F9 settings menu
- All settings persisted via PlayerPrefs
