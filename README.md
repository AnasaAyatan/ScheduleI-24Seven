# Schedule I 24/7 — development build

Independent MelonLoader / Harmony prototype for Schedule I IL2CPP, designed around the reported **v0.4.7f12** installation.

## Build status

**Version 0.3.0-alpha — source code only; not compiled or validated in-game.** This branch is experimental. Do not publish this as a working replacement yet.

| Feature | Implementation | Reliability |
|---|---|---|
| Employee work at end of day | Harmony prefix on parameterless `Employee.CanWork()`, with runtime end-of-day detection | Experimental; bypasses other eligibility checks |
| Plant growth while game time is frozen | Uses discovered `Plant.MinPass(int)` and generic Unity plant enumeration | Experimental; disabled by default, may be unavailable on IL2CPP |
| Wage deduction every 24 minutes during freeze | Experimental 24-minute real-time cycle with dry-run; OFF by default | Unverified duplicate-charge/host authority safety |
| Normal game hours | Employee prefix falls through to original implementation | Unverified |
| Safety | Fail-closed method detection, exceptions logged, failing plant simulation disabled | Requires game testing |

## Local prerequisites

- Steam game version matching your locally generated IL2CPP wrappers
- MelonLoader 0.7.3
- .NET 6 SDK
- The repository's NuGet references; game-specific generated wrappers may require additional local-only references

## Build

From the repository root in PowerShell:

```powershell
git fetch origin
git switch development/24-seven-runtime
dotnet restore
dotnet build -c Release
```

The build may fail without the locally generated game/Unity IL2CPP assembly references. Do **not** commit game or MelonLoader binaries. See your local `Schedule I/MelonLoader/Il2CppAssemblies` directory and share compile errors instead.

## Test carefully

1. Back up your save.
2. Remove the older conflicting `Employee Always Work & Plants Always Grow v2.0.0.dll` from `Mods`.
3. Install the built DLL in `Mods`.
4. Leave wage charging disabled (`EndOfDayWages=false`) until validated; for diagnostics set `EndOfDayWages=true`, `WageDryRun=true`, and remain at 4 AM for 24 real minutes.
5. Plant growth is opt-in (`PlantGrowthAtEndOfDay=true`). Test only with a backed-up save; the reflection-based IL2CPP enumerator can fail.
6. Inspect `MelonLoader/Latest.log` for patch and API-detection messages.
5. Check 4 AM behavior with one employee before testing plants.
6. To opt into plant ticking, set `PlantGrowthAtEndOfDay` to `true` in MelonPreferences, then restart. **This may not work with generated IL2CPP reflection wrappers.**

## Configuration

Category: `ScheduleI24Seven`

- `EmployeesAtEndOfDay`: true
- `PlantGrowthAtEndOfDay`: false
- `Diagnostics`: true
- `EndOfDayWages`: false (turn on only for dry-run testing first)
- `WageDryRun`: true (does not deduct wages)
- `SecondsPerPlantMinute`: 60

## Remaining work

- Verify actual current game end-of-day API and signatures
- Safely retain employee bed, wage and assignment requirements
- Validate wage dry-run first, then verify daily-wage deduction semantics and multiplayer host authority before enabling real charges
- Confirm IL2CPP plant enumeration and growth simulation
- Implement once-per-24-minute wage deductions with no duplicate charges
- Compile with local game DLLs and test single-player/multiplayer host authority

## Attribution and licensing

Independent implementation. No source or game binaries copied from the original Nexus release. No reuse license granted; all rights reserved until the repository owner selects one.
