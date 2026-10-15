# Schedule I 24/7

Independent **Schedule I** (IL2CPP) MelonLoader mod for keeping employees working during the end-of-day 4 AM freeze, with opt-in experimental plant growth.

## Current status — 0.3.1-alpha

Test environment: Schedule I **0.4.7f12** and MelonLoader **0.7.3 Open-Beta** on Windows 11.

| Feature | Status |
|---|---|
| Employees continue working during the 4 AM freeze | **Confirmed in user testing** |
| Plants progress during the 4 AM freeze | **Experimental, disabled by default; not yet verified in-game** |
| Wages at normal day transition | **Unmodified game behavior** |
| Extra wages every 24 minutes at 4 AM | **Not present** |

**Wage policy:** This mod **does not modify payroll**. It does not call `Employee.RemoveDailyWage()`, change `Employee.PaidForToday`, intercept wage-deduction functions, or start any additional pay timer. The game remains responsible for its normal day-transition payroll. That transition's behavior should still be regression-tested in game.

The employee patch overrides `Employee.CanWork()` during detected end-of-day and may bypass other normal eligibility checks. Do not treat this alpha as a stable release.

## Requirements

- Schedule I IL2CPP version used for testing: 0.4.7f12
- MelonLoader 0.7.3
- .NET 6 SDK for building
- NuGet connectivity for dependencies

## Build and install (PowerShell)

Close the game, open the repository root, and run:

```powershell
git switch development/24-seven-runtime
git pull --ff-only origin development/24-seven-runtime
dotnet build -c Release

$game = "C:\Program Files (x86)\Steam\steamapps\common\Schedule I"
Copy-Item ".\bin\Release\net6.0\ScheduleI24Seven.dll" "$game\Mods" -Force
```

If your Steam library is elsewhere, modify `$game`. Build success alone does not prove runtime compatibility. Some NuGet packages currently generate version/framework compatibility warnings.

**Remove** the older `Employee Always Work & Plants Always Grow v2.0.0.dll` from the game's `Mods` folder; it fails on the removed `Employee.GetBed()` API and conflicts with this mod. Keep backups outside the `Mods` folder. Back up your save before testing.

## Preferences

MelonLoader settings: `UserData\MelonPreferences.cfg`, category `ScheduleI24Seven`.

| Setting | Default | Meaning |
|---|---|---|
| `EmployeesAtEndOfDay` | `true` | Allow employees to keep working during the detected freeze |
| `PlantGrowthAtEndOfDay` | `false` | Opt into experimental simulated plant minutes |
| `SecondsPerPlantMinute` | `60` | Real seconds per simulated plant minute |
| `Diagnostics` | `true` | Log detected game APIs and patch status |

**There are no wage-related settings.** If an older settings file contains `EndOfDayWages` or `WageDryRun`, those entries are ignored; you can remove them.

## Plant growth test preset

The repository includes [`config/plant-growth-test.cfg`](config/plant-growth-test.cfg) as an **example**, with plant simulation enabled and employees enabled. It does not activate automatically when you pull the repository, and it is **not** a replacement for your entire MelonPreferences file.

To test, close the game, open `Schedule I/UserData/MelonPreferences.cfg`, and update only these entries inside `[ScheduleI24Seven]`:

```ini
EmployeesAtEndOfDay = true
PlantGrowthAtEndOfDay = true
SecondsPerPlantMinute = 60
Diagnostics = true
```

Back up your save before enabling experimental plant ticking. The mod does not change the game's day-transition wage behavior.

## Test plan

1. Confirm employees continue working through 4 AM.
2. On a **backed-up save**, opt into plant growth and check for growth plus log entries; the IL2CPP enumerator may not work on all installations.
3. Finish the day and check that normal daily wages are charged by the game, with no extra charge from this mod.
4. Check behavior after save/reload, scene changes, and multiplayer host/client transitions.

## Known limitations

- Plant growth is not yet validated and deliberately starts disabled.
- End-of-day detection observes `TimeManager.IsEndOfDay` via a Harmony postfix; it expires stale observations after three seconds.
- Employee `CanWork()` override may bypass non-time checks, including assignment or payment eligibility. Additional compatibility work is planned.
- Generated Unity/IL2CPP reflection types may cause log warnings during startup.

## Developer workflow

`scripts/push-to-github.ps1` now synchronizes an existing branch without reinitializing Git. Optional `-Push` pushes local commits. This script does **not** create the repository or make commits automatically.

## Ownership

Copyright © 2026 AnasaAyatan. All rights reserved. See [LICENSE-NOTE.md](LICENSE-NOTE.md). No game binaries or third-party mod source are included.
