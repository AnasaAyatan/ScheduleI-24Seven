# Schedule I 24/7 — frozen growth and watering test

**Experimental version 0.4.8-alpha**, based on Schedule I v0.4.7f12 / MelonLoader v0.7.3. Not yet compiled or in-game tested.

## Intended end-of-day behavior (4 AM)

- Employees remain eligible to work during the freeze (already user-confirmed in older build).
- Existing employee watering tasks continue normally. **This mod does not command bots to seek pots or automatically refill them**; those features depend on the game's assigned botanist behavior.
- If a bot waters a pot, water may increase toward 100%.
- During the frozen period, the mod attempts to suppress `Pot.OnMinPass()` rather than patching an unpatchable IL2CPP field accessor. This is an experimental pot-minute hook, not a guarantee that all moisture drain paths are blocked. It may also interrupt other pot minute processing.
- Plant growth uses `Plant.NormalizedGrowthProgress` and `Plant.SetNormalizedGrowthProgress(float)` without calling `Plant.MinPass(int)` when water protection is enabled. Growth rate is experimental and must be calibrated.
- No wage modifications. The game handles wages on its normal day transition.

## Build & install

Close the game and back up your save before testing.

```powershell
git fetch origin
git switch development/frozen-growth-watering
dotnet build -c Release
$game = "C:\Program Files (x86)\Steam\steamapps\common\Schedule I"
Copy-Item ".\bin\Release\net6.0\ScheduleI24Seven.dll" "$game\Mods" -Force
```

Remove the older, conflicting Employee Always Work & Plants Always Grow DLL from Mods.

## Preferences

In `Schedule I/UserData/MelonPreferences.cfg`, under `[ScheduleI24Seven]`:

```ini
EmployeesAtEndOfDay = true
PlantGrowthAtEndOfDay = true
ProtectWaterDuringFrozenGrowth = true
SecondsPerPlantMinute = 30
NormalizedGrowthPerMinute = 0.001
Diagnostics = true
```

**Water must be added by normal gameplay/bots.** This prototype does not instantly fill or refill any pot, and it should never decrease water itself when using the growth-only route. The effect of the game's own growth setter on other state has not been independently verified.

## Test checklist

1. Compile and confirm there are no build errors.
2. At 4 AM, inspect logs for `Patched Pot.OnMinPass()`, then `Advanced ... plants`.
3. Select a pot below 100% with a properly assigned botanist. Observe whether the botanist waters it as usual.
4. Compare moisture before/after one minute of frozen growth, and again after the pot becomes full. Report any decreases.
5. Test a full day transition and verify regular payroll remains unchanged.
6. If moisture decreases despite the patch, revert to the 0.3.4-alpha branch/build and report log details.

## Limitations

`SetNormalizedGrowthProgress` may have internal visual/network side effects; the numeric growth rate is an adjustable prototype. The moisture patch targets `Pot.OnMinPass()`; other direct fields, RPC replication, and game subsystems may still affect moisture. The botanist's decision to water pots has not been patched and must be tested separately. It may need a host-authority guard for multiplayer. The IL2CPP plant array reflection fallback is also untested.

Copyright © 2026 AnasaAyatan. All rights reserved. No game binaries included.

## 0.4.8-alpha test interpretation

- The previous build logged 36 successful calls but the user observed **no visible plant growth**. This version increases the default experimental progress rate to 0.01 per simulated minute and logs a before/requested/after growth readback on the first updated plant. Existing preference values override new defaults, so edit `NormalizedGrowthPerMinute = 0.01` explicitly to test it.
- The earlier accessor patch was rejected by MelonLoader. This version hooks the real `Pot.OnMinPass()` method during end-of-day, but has **not been runtime tested**.
- Botanist task selection remains vanilla; there is no new employee task-search algorithm. Employee continued work eligibility is patched, but bots watering to 100% is not yet established.
- Wages are untouched.

## 0.4.8-alpha: plant-specific growth and small boost

After 4 AM, the experimental growth-only path now tries to calculate each plant's growth increment using its own GrowthTime, light exposure, temperature, and pot growth multiplier, with a configurable **5%** boost. The default new setting is `AfterHoursGrowthMultiplier = 1.05` (any positive finite value accepted). This implementation depends on reflection-based game APIs and has **not yet been built or validated in-game**. If a required API cannot be used, it logs a warning and falls back to the previously working `NormalizedGrowthPerMinute` rate (also boosted 5%). For a test run, keep `ProtectWaterDuringFrozenGrowth = true`.

The mod does not yet independently make botanists choose partially filled pots for watering. Payroll code remains unchanged; no extra wage charges are made.

## 0.4.8-alpha multiplier change

`AfterHoursGrowthMultiplier` is no longer capped at 1.25. A value of `3` requests 3x growth during the 4 AM freeze; `1.05` requests a 5% boost. Invalid (zero, negative, NaN, or infinity) values are replaced with `1.0` at runtime. Values greater than 2 log a one-time warning. Individual growth increments remain constrained to a valid [0, 1] progress interval. Not yet tested in game.

## 0.4.8-alpha JSON settings (experimental)

The mod creates `Schedule I/UserData/ScheduleI24Seven.json` at first run, importing existing MelonPreferences values. It reads updates to that file about every 2 seconds and applies them in game. Example:

```json
{
  "EmployeesAtEndOfDay": true,
  "PlantGrowthAtEndOfDay": true,
  "ProtectWaterDuringFrozenGrowth": true,
  "Diagnostics": true,
  "SecondsPerPlantMinute": 30,
  "RealSecondsPerGameMinute": 1,
  "NormalizedGrowthPerMinute": 0.01,
  "AfterHoursGrowthMultiplier": 1.05
}
```

**In-game Mod Manager:** On the Schedule I 24/7 screen, enter the full path to `ScheduleI24Seven.json` and select **LOAD**. That manager's discovery/editor compatibility is external and not yet verified. The JSON hot reload works independently if the game's phone editor cannot save the file. JSON becomes the authoritative configuration once present; changes made only to `MelonPreferences.cfg` after first launch may be overridden by JSON.

**Growth-time fix:** v0.4.4 introduced `RealSecondsPerGameMinute = 1`, converting 30 elapsed real seconds to about 30 game minutes during the 4 AM freeze. The native plant-rate calculation uses this elapsed time and the configured multiplier. This is experimental and can be fast; back up your save, and start testing at `AfterHoursGrowthMultiplier = 1.05`. If native game rate APIs fail, the fallback retains older pacing. Botanist watering task selection remains unresolved. Native wages remain unchanged.

## v0.4.8-alpha compatibility fix

Replaced the unavailable `MelonEnvironment.UserDataDirectory` symbol with `Path.Combine(AppContext.BaseDirectory, "UserData")` for compilation against the current MelonLoader package. Confirm the logged JSON configuration path on startup; if the game's runtime base directory differs from the game root, the generated file may be placed elsewhere. Rebuild before reinstalling: a failed build leaves the previous DLL available in `bin/Release`.

## 0.4.8-alpha: native in-game settings panel

The mod now registers its MelonPreferences under a named **Schedule I 24/7** settings category, with descriptions and boolean/float controls. This is intended to make the **Mod Manager & Phone App** render the same toggle/input UI as other mods when selecting Schedule I 24/7, without manually loading a JSON path.

Phone settings persist in the game's `UserData/MelonPreferences.cfg`, and the mod mirrors live preference changes to `UserData/ScheduleI24Seven.json` approximately every two seconds. Existing JSON settings are imported at startup, preserving previous configurations. JSON file edits are still hot-reloaded. The UI auto-discovery behavior must be confirmed in game with Mod Manager & Phone App v2.2.4. This change does not fix NPC watering logic or alter wage handling.

## 0.4.8-alpha experimental botanist watering

Adds the optional **Continue botanist watering after 4 AM** phone setting (`BotanistWateringAfter4AM`, default false). The patch attempts to queue the game's actual watering behaviour for an idle, server-authoritative botanist using normal pot selection; it does not set a pot's moisture value directly, and does not touch employee wages. API discovery is fail-closed, logging which game members are missing. The build and actual NPC watering remain unverified. Before testing, back up your save, build, install, restart the game, and check the MelonLoader log for either a patch-installed message or an incomplete-API warning. Only enable this experimental option after startup is clean.
