# Schedule I 24/7 (compatibility prototype)

Independent, source-first project for Schedule I **IL2CPP** on Windows with MelonLoader.

## Status — 0.1.0-alpha

**Not a drop-in replacement yet. Not tested or built against game version 0.4.7f12.**

| Original feature | Current implementation |
|---|---|
| Employees allowed to work at end-of-day | Harmony `Employee.CanWork` prefix, guarded by reflective end-of-day detection; **untested** |
| Plants grow during 4 AM time freeze | Experimental tick scaffold; **NOT functional / disabled** until IL2CPP plant enumeration is validated |
| Employees collect wages every 24 minutes during end-of-day | **NOT implemented** (must find safe game payment API to avoid double-charging) |
| Normal daytime behaviour unchanged | Prefix falls through when end-of-day is not detected |

The old release calls `Employee.GetBed()` and crashes with `MissingMethodException` under v0.4.7f12. This source avoids that removed method, but is **not** yet proof of compatibility or equivalent features.

## Prerequisites

- Steam Schedule I, default IL2CPP branch (user log reports v0.4.7f12)
- MelonLoader 0.7.3 (user log)
- .NET 6 SDK for compiling
- `LavaGang.MelonLoader` and `Lib.Harmony` NuGet packages

## Build (PowerShell)

```powershell
dotnet restore
dotnet build -c Release
```

If NuGet package resolution or game-specific Unity reference errors occur, reference the assemblies generated on **your own PC** in `Schedule I\MelonLoader\Il2CppAssemblies` and `Schedule I\MelonLoader\net6`, and adjust `ScheduleI24Seven.csproj`. Do not commit redistributed game DLLs.

## Install for testing

1. Back up your save.
2. Remove `Employee Always Work & Plants Always Grow v2.0.0.dll` from `Schedule I\Mods` to prevent conflicts.
3. Build the project, then place `bin\Release\net6.0\ScheduleI24Seven.dll` in the game's `Mods` folder.
4. Launch and inspect `MelonLoader\Latest.log` for `24/7 alpha initialized` and `Patched` lines.
5. Verify employee behaviour during the 4 AM freeze, not just at startup. Do not enable plant ticking yet.

## Configuration

MelonPreferences writes preferences in `UserData\MelonPreferences.cfg`.

- `EmployeesAtEndOfDay` — true (experimental)
- `PlantGrowthAtEndOfDay` — false until IL2CPP enumeration is completed
- `Diagnostics` — true
- `SecondsPerPlantMinute` — 60

## Compatibility TODOs

- [ ] Inspect `Il2CppScheduleOne.GameTime.TimeManager` in v0.4.7f12 and determine actual end-of-day flag.
- [ ] Confirm `Employee.CanWork` signature and verify existing bed and wage eligibility checks.
- [ ] Implement plant enumeration and `MinPass` equivalent via supported generated IL2CPP types.
- [ ] Locate and safely invoke game's wage mechanism every 24 game minutes during end-of-day; test against double deductions.
- [ ] Multiplayer host/client authority tests; save/reload regression tests.
- [ ] Compile and test a release artifact against v0.4.7f12.

## Attribution

Independent prototype; **does not incorporate code or assets from the Nexus mod**. References for research: [Schedule I IL2CPP example](https://github.com/EndureBlackout/schedule-il2cpp-example), [PlantsAlwaysGrow](https://github.com/lasersquid/Sched1PlantsAlwaysGrowMod).

## License

No public reuse license has been granted. All rights reserved pending the owner's choice of license.
