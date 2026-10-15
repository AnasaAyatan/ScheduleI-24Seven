# Schedule I 24/7 — frozen growth and watering test

**Experimental version 0.4.0-alpha**, based on Schedule I v0.4.7f12 / MelonLoader v0.7.3. Not yet compiled or in-game tested.

## Intended end-of-day behavior (4 AM)

- Employees remain eligible to work during the freeze (already user-confirmed in older build).
- Existing employee watering tasks continue normally. **This mod does not command bots to seek pots or automatically refill them**; those features depend on the game's assigned botanist behavior.
- If a bot waters a pot, water may increase toward 100%.
- While the game reports end of day, a guarded Harmony patch attempts to prevent decreases routed through `Pot._currentMoistureAmount` setter. It **does not block other write paths**, so water preservation is NOT guaranteed yet.
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
2. At 4 AM, inspect logs for `Patched Pot moisture setter`, then `Advanced ... plants`.
3. Select a pot below 100% with a properly assigned botanist. Observe whether the botanist waters it as usual.
4. Compare moisture before/after one minute of frozen growth, and again after the pot becomes full. Report any decreases.
5. Test a full day transition and verify regular payroll remains unchanged.
6. If moisture decreases despite the patch, revert to the 0.3.4-alpha branch/build and report log details.

## Limitations

`SetNormalizedGrowthProgress` may have internal visual/network side effects; the numeric growth rate is an adjustable prototype. The moisture patch only intercepts the discovered property setter, not direct fields, RPC replication, or all other game subsystems. It may need a host-authority guard for multiplayer. The IL2CPP plant array reflection fallback is also untested.

Copyright © 2026 AnasaAyatan. All rights reserved. No game binaries included.
