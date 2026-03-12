# Claude instructions — m0n0t0ny's mod (Escape from Duckov)

## After EVERY code change

1. **Build** (specify the csproj to avoid ambiguity):
   ```
   cd "c:/Users/antob/Desktop/Escape From Duckov/duckov_modding/m0n0t0nysMod" && dotnet build m0n0t0nysMod.csproj -c Release
   ```

2. **Install** the DLL (game must be closed):
   ```
   powershell -Command "Copy-Item 'c:\Users\antob\Desktop\Escape From Duckov\duckov_modding\m0n0t0nysMod\bin\Release\netstandard2.1\m0n0t0nysMod.dll' -Destination 'C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\m0n0t0nysMod\m0n0t0nysMod.dll' -Force"
   ```

3. **Verify** the install succeeded (check size and timestamp changed):
   ```
   powershell -Command "Get-Item 'C:\Program Files (x86)\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\m0n0t0nysMod\m0n0t0nysMod.dll' | Select-Object Length, LastWriteTime | Format-List"
   ```
   If the copy fails with "mapped section" error, the game is still open — ask the user to close it first.

## Whenever a feature is added, changed, or removed

4. **Update `info.ini` description** in both locations:
   - `m0n0t0nysMod/ReleaseExample/m0n0t0nysMod/info.ini`
   - `C:/Program Files (x86)/Steam/steamapps/common/Escape from Duckov/Duckov_Data/Mods/m0n0t0nysMod/info.ini`
   - Format: spaces escaped with `\ `, features separated by ` | `

5. **Bump the version** in both `info.ini` files and in the `Mod info` table below (`version = X.Y`).
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
| Technical name (folder, DLL, `name` in info.ini) | `m0n0t0nysMod` |
| Namespace | `m0n0t0nysMod` |
| Display name | `m0n0t0ny's mod` |
| Settings key | F9 |
| Current version | 1.0 |

## Feature list (keep in sync with info.ini description)

- Shows item sell value on hover — always, not just in shops
- Display mode: single price only / stack total only / combined (single / stack)
- Sleep preset buttons: wake at 2 custom configurable times, until rain, Storm I, Storm II, post-storm
- F9 opens settings menu with all toggles and configurable preset times

---

## Changelog

### v1.0
- Initial release
- Item sell value on hover (always active)
- Single / stack / combined display modes
- Sleep preset buttons (6 presets: 2 custom times + rain + Storm I/II + post-storm)
- Sleep presets configurable from F9 settings menu
- All settings persisted via PlayerPrefs
