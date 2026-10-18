# Schedule I 24/7

Keep employees working and plants growing after **4 AM** in [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/).

Schedule I normally pauses parts of the simulation at the end of the day. Schedule I 24/7 keeps employees working and lets plants continue growing after 4 AM. Botanists can keep watering their assigned pots through normal in-game behavior. The mod does **not** instantly refill pots.

**Release candidate:** 0.6.0 (unreleased; latest published version is 0.5.0)  
**Tested with:** Schedule I 0.4.7f12, MelonLoader 0.7.3 Open-Beta, Mod Manager & Phone App 2.2.4  
**Status:** v0.6.0 feature-branch candidate. User-reported tests passed for auto jobs, manual transactions, reserve, save/reload and UI. A clean final release build is pending. The [published v0.5.0 release](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.5.0) remains available.

## Features

- **After-hours employees:** Continue employee work during the 4 AM freeze.
- **After-hours botanists:** Idle botanists may take normal watering jobs for their assigned pots.
- **After-hours plant growth:** Continue plant-specific growth after 4 AM, taking native growth factors into account when available.
- **Water protection:** Prevent the normal frozen-time pot minute update from draining moisture, without generating water.
- **Adjustable growth speed:** Set any **positive, finite** growth multiplier (for example, `1` for 1×, `3` for 3×, `10` for 10×). No artificial multiplier or per-update simulated-minute cap.
- **Optional automatic laundering:** Start native jobs within business capacity and cash reserve; OFF by default.
- **Manual laundering compatibility:** Automatic jobs restore the previous terminal selection.
- **In-game settings:** Edit settings through the compatible phone Mod Manager.

> **Growth multiplier warning:** Very high values (such as 10× or more) can make plants mature almost instantly and skip visible growth stages. There is no artificial multiplier cap, but total plant progress remains within 0–100%. Start low and increase gradually.

## Requirements

- Windows version of Schedule I
- MelonLoader 0.7.3 Open-Beta (the version used for testing)
- **Optional:** Mod Manager & Phone App 2.2.4 for convenient in-game controls

The mod uses IL2CPP interfaces from the tested game version. Game updates may require compatibility changes.

## Install

1. Install MelonLoader for Schedule I and launch the game once.
2. Close the game.
3. Download [ScheduleI24Seven-v0.5.0.zip](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/download/v0.5.0/ScheduleI24Seven-v0.5.0.zip) from the official release and extract `ScheduleI24Seven.dll`, or build it from source.
4. Copy it into the game's `Mods` folder, for example:
   `C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\`
5. Remove other mods that independently override the same end-of-day employee or plant behavior, to avoid conflicts.
6. Restart the game.

**Building from source:**

```powershell
git clone https://github.com/AnasaAyatan/ScheduleI-24Seven.git
cd ScheduleI-24Seven
git switch feature/auto-laundering
dotnet build -c Release
```

After a successful build, the DLL is in `bin\Release\net6.0\ScheduleI24Seven.dll`. Close the game before replacing the installed DLL.

## Automatic laundering defaults and cash reserve

| Phone setting | Fresh-install default | Behaviour |
| --- | --- | --- |
| Auto-start laundering | **OFF** | Never automatically spend cash until enabled |
| After-hours time speed | **1.0** | Normal speed |
| Max per automatic job ($) | **0** | Full available business capacity |
| Cash to keep ($) | **0** | No reserve; choose a positive amount to protect carried cash |

**Max per automatic job** limits each job, not the total spent across owned businesses. For example, $10,000 carried cash and a $2,000 reserve leave at most $8,000 for automatic transactions. Manual laundering is not governed by this reserve. Existing saved preferences are retained.

## In-game settings layout (v0.6.0)

The Phone Mod Manager preferences are organized into distinct sections: **Workers**, **Plants**, **Laundering**, and **Advanced**. This matches the separate sections in the editable JSON file while keeping short, readable phone labels. The first three sections contain the corresponding gameplay features; Advanced contains shared timing and diagnostics.

The new preference categories have new internal identifiers. On first launch, the mod loads existing settings from `UserData/ScheduleI24Seven.json`, including legacy `Employees` and flat JSON keys. Back up both the JSON file and `UserData/MelonPreferences.cfg` before updating. If the phone manager shows obsolete entries from the old category, those entries come from the saved legacy preferences and should not be edited; verify migration before cleaning them.

## Configure in game

With **Mod Manager & Phone App**, open **Phone → Mod Manager → Schedule I 24/7**, adjust settings and select **SAVE**. Phone settings sync to `UserData/ScheduleI24Seven.json`.

| Section | Controls |
| --- | --- |
| Workers | Keep workers active after 4 AM; Keep botanists watering |
| Plants | Keep plants growing after 4 AM; Protect plant moisture |
| Laundering | Finish jobs after 4 AM; Auto-start laundering; Max per automatic job ($); Cash to keep ($) |
| Advanced | After-hours time speed; Plant update interval; After-hours growth multiplier; Fallback plant growth rate; Troubleshooting logs |

The growth multiplier defaults to 1.05; 1 is standard and 2 is double. The plant update interval defaults to 60 seconds. After-hours time speed defaults to 1.0 (normal); smaller positive values accelerate the simulation. Saved user settings may differ.

## Automatic laundering (v0.6.0 release candidate)

Automatic laundering is **OFF by default**. Once enabled, the scheduler checks owned business capacity, existing jobs, carried cash and reserved cash. It rotates between businesses, starts at most one job per scheduling pass and waits for native confirmation (cash debit plus a new operation) before proceeding. The per-job cap does not set a global laundering budget.

Automatic job creation uses the game's laundering interface and restores its previous business selection after a successful submission, preserving manual terminal use. Uncertain transactions halt further automatic submissions for the session. In-progress jobs continue after the game's 4 AM freeze if **Finish jobs after 4 AM** is enabled.

**Verification:** The user reports passing multi-business auto-job, manual compatibility, cash reserve, save/reload, and concurrent manual/auto tests. Final build and static regression tests still need to run before release. Multiplayer and extended unattended operation have not been independently verified.

**Troubleshooting:** Search `MelonLoader/Latest.log` for `Auto laundering`, `Game auto request`, and `halted`. If a request halts, disable auto-start and inspect the log instead of retrying on a valuable save.

## Optional JSON configuration

You do **not** need to find or enter a JSON path in the phone menu.

The mod creates and synchronizes:

`<Schedule I game folder>\UserData\ScheduleI24Seven.json`

For advanced editing, open that file in a text editor. For example:

```json
{
  "EmployeesAtEndOfDay": true,
  "BotanistWateringAfter4AM": true,
  "PlantGrowthAtEndOfDay": true,
  "ProtectWaterDuringFrozenGrowth": true,
  "Diagnostics": false,
  "SecondsPerPlantMinute": 30,
  "RealSecondsPerGameMinute": 1,
  "NormalizedGrowthPerMinute": 0.01,
  "AfterHoursGrowthMultiplier": 3
}
```

The JSON file is organized into `Workers`, `Plants`, `Laundering`, `Simulation`, and `Advanced` objects. For example:

```json
{
  "Workers": {
    "EmployeesAtEndOfDay": true,
    "BotanistWateringAfter4AM": true
  },
  "Plants": {
    "PlantGrowthAtEndOfDay": true,
    "ProtectWaterDuringFrozenGrowth": true,
    "SecondsPerPlantMinute": 30,
    "NormalizedGrowthPerMinute": 0.01,
    "AfterHoursGrowthMultiplier": 3
  },
  "Laundering": {
    "EnableAutoLaunderTransactions": false,
    "AutoLaunderBusinessCap": 0,
    "MinimumCashReserve": 0,
    "LaunderingProgressAfter4AM": true
  },
  "Simulation": {
    "RealSecondsPerGameMinute": 1
  },
  "Advanced": {
    "Diagnostics": false
  }
}
```

Older files with an `Employees` section or flat setting names are still accepted. New JSON exports use `Workers`. Make a backup before manually editing; the phone Mod Manager can also update these settings.

These are **example preferences**, not necessarily the defaults in a fresh installation. Existing settings are preserved when upgrading. JSON changes are checked approximately every two seconds; the phone settings are stored in `UserData\MelonPreferences.cfg` and mirrored to JSON.

Only use positive, finite numbers for the multiplier and timing settings. `NormalizedGrowthPerMinute` may be zero or positive. Invalid settings are ignored or replaced with safe runtime fallbacks.

## How watering works

Watering uses the game's botanist behavior and available watering supplies. Bots must be assigned to the appropriate pots and able to reach them. The mod does **not** teleport water into pots or automatically set pot moisture to 100%.

When water protection is enabled, the mod skips `Pot.OnMinPass()` after 4 AM. This prevents that minute-based moisture drain, but it also pauses **other pot minute logic** during the freeze; this is a known implementation trade-off.

## Troubleshooting

**Settings missing in the phone:** Check that Mod Manager & Phone App is installed and you are running a recent build of Schedule I 24/7. You can still edit `UserData\ScheduleI24Seven.json` directly.

**Employees do not work:** Verify `EmployeesAtEndOfDay` is enabled. Other mods and employee assignment restrictions may affect behavior.

**Botanists do not water:** Enable `BotanistWateringAfter4AM`; check that watering supplies are available and pots are assigned. If needed, enable Diagnostics and inspect `MelonLoader\Latest.log` for the botanist hook message.

**Plants do not grow:** Enable `PlantGrowthAtEndOfDay`; confirm the freeze has started and look for any missing-API or plant-update warnings. Try a small, reasonable multiplier to verify baseline operation.

**Build fails:** Do not copy an older DLL from `bin\Release` after a failed build. Fix the compiler errors first.

## Compatibility and limitations

- Developed against Schedule I v0.4.7f12, with MelonLoader v0.7.3 Open-Beta.
- Native growth calculation depends on the game's growth APIs. A configurable fallback is available.
- Extremely high multipliers can advance plants to maturity in a single refresh.
- The end-of-day employee work patch alters `Employee.CanWork()` during the freeze; this may bypass some non-time work checks.
- The water-protection hook suppresses the whole pot minute update during the freeze, not only moisture drain.
- Multiplayer host/server behavior should be tested separately.

## Releases and history

- [Changelog](CHANGELOG.md) — changes by version.
- [Release notes](RELEASE_NOTES.md) — features, compatibility and installation details.

**v0.5.1 is in development and has not been published.** The latest downloadable release is [v0.5.0](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.5.0).

## License

Copyright © 2026 AnasaAyatan. All rights reserved. See [LICENSE](LICENSE) for the proprietary personal-use terms. Free, unmodified, non-commercial gameplay use of the officially distributed DLL is permitted; redistribution, modification, and commercial use require prior written permission, except where applicable law provides otherwise. This is **not an open-source license**.

## Support

When reporting an issue, include the mod version, game version, what happened, relevant settings, and the MelonLoader log.

Copyright © 2026 AnasaAyatan. All rights reserved. No game binaries included.
