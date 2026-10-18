# Schedule I 24/7

**Keep your Schedule I business running after 4 AM.** Continue employee tasks, botanist watering, plant growth and laundering progress when the in-game clock stops. Optionally automate laundering at owned businesses.

**Latest release:** [v0.6.0](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.6.0) · **Game tested:** Schedule I 0.4.7f12 · **Platform:** Windows / IL2CPP

## Features

- **Workers after 4 AM:** Employees can continue their assigned work while the clock is stopped.
- **Botanist watering:** Botanists can continue normal watering tasks for assigned pots when supplies are available.
- **Plant growth and moisture protection:** Plants continue growing; pots do not lose moisture through the usual frozen-time minute update.
- **Laundering progress after 4 AM:** Existing laundering operations can continue advancing.
- **Optional automatic laundering:** Start jobs at eligible owned businesses, respecting available capacity, carried cash and a configurable reserve. Manual business terminals remain usable.
- **Phone settings:** Simple Workers, Plants, Laundering and Advanced sections using Mod Manager & Phone App.

## Requirements

- Schedule I for Windows (tested against **0.4.7f12**)
- [MelonLoader](https://github.com/LavaGang/MelonLoader) **0.7.3 Open-Beta** (tested version)
- Optional: **Mod Manager & Phone App 2.2.4** for changing settings in-game

Newer game and loader versions are not guaranteed compatible.

## Installation

1. Install MelonLoader for Schedule I and launch the game once.
2. Close the game.
3. Open the [latest release](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.6.0) and download **ScheduleI24Seven-v0.6.0.zip** from **Assets**. If the ZIP is not listed, the release binary has not yet been attached; source-code archives are **not** ready-to-install mod packages.
4. Extract **ScheduleI24Seven.dll** from the ZIP.
5. Put the DLL in your game's `Mods` folder, normally:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Mods\
   ```

6. Start Schedule I. With the optional phone app, go to **Phone → Mod Manager → Schedule I 24/7**.

**Upgrading:** Replace the old DLL, not your save or settings files. Back up your save and `UserData/ScheduleI24Seven.json` and `UserData/MelonPreferences.cfg` first. Existing saved preferences take precedence over fresh-install defaults.

## Settings

| Section | Option | Default | Purpose |
| --- | --- | --- | --- |
| Workers | Keep workers active after 4 AM | ON | Continue assigned work |
| Workers | Keep botanists watering | ON | Continue normal watering tasks |
| Plants | Keep plants growing after 4 AM | ON | Continue growth |
| Plants | Protect plant moisture | ON | Suppress frozen-time pot moisture drain |
| Laundering | Finish jobs after 4 AM | ON | Advance existing laundering operations |
| Laundering | Auto-start laundering | **OFF** | Opt in to automatic jobs |
| Laundering | Max per automatic job ($) | **0** | 0 = full eligible business capacity |
| Laundering | Cash to keep ($) | **0** | Amount of carried cash automatic jobs must leave untouched |
| Advanced | After-hours time speed | **1.0** | Normal simulation speed; smaller positive values are faster |
| Advanced | Plant update interval (seconds) | 60 | Time between plant updates |
| Advanced | After-hours growth multiplier | 1.05 | 1 = baseline growth, 2 = double |
| Advanced | Fallback plant growth rate | 0.01 | Used when native calculation is unavailable |
| Advanced | Troubleshooting logs | OFF | Extra diagnostic logging |

**Important:** The maximum-per-job setting is **not** a global spending budget. For example, if you carry $10,000 and set **Cash to keep** to $2,000, automatic jobs can use at most $8,000 total until your cash changes, subject to per-business capacity.

### How automatic laundering works

When enabled, the mod checks owned businesses for available capacity, finds eligible cash above the reserve and submits at most one new job per scheduling pass. It confirms both the cash change and operation creation before starting another job. If a submission cannot be verified, automatic requests stop for that session to avoid duplicates. The mod also restores the previous laundering-terminal business selection after automatic submission.

**Automatic laundering is OFF on fresh installs.** Manual laundering is not limited by the mod's cash-reserve setting.

## Configuration and troubleshooting

Phone settings are mirrored to:

```text
<Schedule I game folder>\UserData\ScheduleI24Seven.json
```

The JSON file groups values under `Workers`, `Plants`, `Laundering`, `Simulation` and `Advanced`. Older flat keys and the previous `Employees` section remain readable. Back up the file before manual edits; don't delete it to reset one setting unless you intend to reset your configuration.

- **Settings missing:** Install the supported Mod Manager & Phone App or use the JSON file.
- **No employee activity:** Check assignments and enable **Keep workers active after 4 AM**.
- **Botanists not watering:** Check their pot assignments, accessible supplies and the watering toggle.
- **Plants not growing:** Check the growth toggle and Advanced timing values.
- **Auto laundering not starting:** Make sure it is enabled, the business is owned and has available capacity, there is eligible cash above your reserve, and no unverified transaction halted the session.
- **Unexpected behaviour:** Enable **Troubleshooting logs** and inspect `MelonLoader/Latest.log`. Disable automatic laundering if a transaction is uncertain.

### Known limitations

- Very high plant-growth multipliers can skip visible growth stages.
- Moisture protection suppresses the entire pot minute update during the freeze, potentially affecting other pot-minute logic.
- The worker check may bypass some non-time work restrictions during the frozen period.
- Multiplayer, long unattended sessions and newer game versions have not been independently verified.
- Watering uses normal game tasks; the mod does **not** instantly refill pots.

## For developers

Requires the .NET 6 SDK. Clone and build:

```powershell
git clone https://github.com/AnasaAyatan/ScheduleI-24Seven.git
cd ScheduleI-24Seven
.\tests\Verify-AutoLaundering.ps1
dotnet build -c Release
```

The DLL is created at `bin/Release/net6.0/ScheduleI24Seven.dll`. See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines, [CHANGELOG.md](CHANGELOG.md) for version history and [RELEASE_NOTES.md](RELEASE_NOTES.md) for v0.6.0 release notes.

## License

© 2026 AnasaAyatan. All rights reserved. See [LICENSE](LICENSE) for personal-use permissions and restrictions. This project is **source-available, not open source** under an OSI-approved license. No game binaries are included.
