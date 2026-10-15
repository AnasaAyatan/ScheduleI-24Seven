# Schedule I 24/7 — experimental development build

Independent MelonLoader/Harmony prototype for Schedule I IL2CPP v0.4.7f12.

## Status: 0.3.1-alpha

- **Employees at 4 AM:** Confirmed by the user to keep working after 4 AM. The work eligibility prefix is experimental and may bypass other requirements.
- **Plants at 4 AM:** Experimental `Plant.MinPass(int)` simulation, disabled by default. Requires a backup save and an in-game test.
- **Wages:** **Unmodified game behavior.** The mod does not charge wages, change `PaidForToday`, or call `RemoveDailyWage`. Wages remain handled by the game's normal day transition. There is no extra 24-minute wage cycle.
- **Compatibility:** MelonLoader 0.7.3 and game v0.4.7f12 were confirmed in logs. This build requires further plant testing.

## Build and install (PowerShell)

```powershell
git pull origin development/24-seven-runtime
dotnet build -c Release
Copy-Item ".\bin\Release\net6.0\ScheduleI24Seven.dll" "C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods" -Force
```

Close the game before copying the DLL. Back up your save and keep the conflicting old Employee Always Work & Plants Always Grow DLL out of the Mods folder.

## Settings

MelonPreferences category: `ScheduleI24Seven`

- `EmployeesAtEndOfDay`: true
- `PlantGrowthAtEndOfDay`: false (experimental; set true for controlled testing)
- `SecondsPerPlantMinute`: 60
- `Diagnostics`: true

Older `EndOfDayWages` and `WageDryRun` lines may remain in an existing preferences file. **They are ignored and may be deleted.**

## Remaining work

- Validate plant growth and its IL2CPP enumerator, and avoid duplicate growth on day transitions.
- Preserve employee eligibility requirements more precisely than overriding `CanWork`.
- Confirm normal wage deductions and day transitions in game (no modification of wage logic).
- Test stability with single-player and multiplayer.

## License

All rights reserved until the owner chooses a license. Do not commit game binaries.
