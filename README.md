# Schedule I 24/7

Keep employees working and plants growing after **4 AM** in [Schedule I](https://store.steampowered.com/app/3164500/Schedule_I/).

Schedule I normally pauses parts of the simulation at the end of the day. Schedule I 24/7 keeps employees working and lets plants continue growing after 4 AM. Botanists can keep watering their assigned pots through normal in-game behavior. The mod does **not** instantly refill pots.

**Development version:** 0.5.1 (unreleased; latest published version is 0.5.0)  
**Tested with:** Schedule I 0.4.7f12, MelonLoader 0.7.3 Open-Beta, Mod Manager & Phone App 2.2.4  
**Status:** v0.5.1 builds locally and its native automatic laundering requests passed an initial four-operation game test; extended unattended and multiplayer behavior are still under test. The [published v0.5.0 release](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.5.0) remains available.

## Features

- **After-hours employees:** Continue employee work during the 4 AM freeze.
- **After-hours botanists:** Idle botanists may take normal watering jobs for their assigned pots.
- **After-hours plant growth:** Continue plant-specific growth after 4 AM, taking native growth factors into account when available.
- **Water protection:** Prevent the normal frozen-time pot minute update from draining moisture, without generating water.
- **Adjustable growth speed:** Set any **positive, finite** growth multiplier (for example, `1` for 1×, `3` for 3×, `10` for 10×). No artificial multiplier or per-update simulated-minute cap.
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

## Configure in game

If you have **Mod Manager & Phone App**, open **Phone → Mod Manager → Schedule I 24/7**. Adjust the switches and number fields, then select **SAVE**. Changes are mirrored to the JSON settings file automatically.

| Phone setting | What it does |
| --- | --- |
| Employees work after 4 AM | Allow employees to continue working during the freeze |
| Allow plants to grow after 4 AM | Continue plant growth after 4 AM |
| Botanists water after 4 AM | Let idle botanists take normal watering tasks |
| Prevent water loss after 4 AM | Suppress frozen-time pot minute updates to protect moisture |
| Plant growth speed multiplier | Multiply simulated plant growth (`1` = 1×, `3` = 3×) |
| Growth refresh interval (seconds) | How often growth is updated during the freeze |
| Seconds per simulated game minute | Conversion between real time and simulated plant-growth time; lower is faster |
| Fallback growth per refresh | Backup progress increment if game-native growth calculation is unavailable |
| Enable debug logging | Add detailed diagnostic information to MelonLoader logs |

**Fresh-install defaults in v0.5.1:** Employee work, plant growth, botanist watering and water protection are all enabled. Existing users retain their saved preferences, including any disabled settings. Start with a growth multiplier of `1` and increase it to your preference.

The refresh interval controls how often updates occur; it isn't intended as a growth multiplier. The growth-speed multiplier and seconds-per-simulated-minute setting control the simulation speed.

## Experimental in-game automatic laundering (v0.5.1 feature branch)

Schedule I 24/7 can use owned businesses to schedule in-game laundering jobs from available carried cash. This is a gameplay feature involving fictional game currency.

- Enable **Check available capacity** and **Experimental automatic jobs** in the phone mod settings. The payment option is off by default; back up your save before enabling it.
- **Maximum per business (0 = full capacity)** limits the size of each scheduled job. For example, `5000` sets a $5,000 in-game per-business ceiling; `0` uses the game's current available business capacity.
- An initial request is capped at $100 per game session until its new operation and cash change are verified; subsequent requests use the selected capacity limit.
- The scheduler selects at most one business per pass, rotates across owned businesses, and waits for a request to be verified before another can begin.
- New jobs are considered when existing operations finish and capacity becomes available. Existing operations can also continue progressing during the game's 4 AM freeze when **Continue after 4 AM** is enabled.
- Switching automatic jobs off prevents future submissions; a previously submitted request can still finish and be checked. If confirmation is uncertain, automated requests halt for the remainder of the session.

**Test coverage:** One local test confirmed four consecutive automatic game requests ($100, $2,000, $5,000, $5,000) for a total of $12,100. Subsequent scheduling updates, continuous unattended operation, multiplayer behavior, and restart behavior after extended 4 AM operation still need validation.

**Troubleshooting:** If a request stops, disable the experimental option and inspect `MelonLoader/Latest.log` for `Auto laundering`, `Game auto request`, and `halted`. Do not repeatedly re-enable transactions on a valuable save after an unconfirmed request.

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
    "AutoLaunderFromInventory": true,
    "EnableAutoLaunderTransactions": false,
    "AutoLaunderBusinessCap": 5000,
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
