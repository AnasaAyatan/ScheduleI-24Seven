# Schedule I 24/7 v0.5.1 — Release Draft

> **Unreleased development build.** Do not publish or merge until the v0.5.1 build is verified and approved. The current public stable release remains [v0.5.0](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.5.0).

## What's changed

All four main after-hours options are now **enabled by default on fresh installations**:

- Employees work after 4 AM — ON (unchanged)
- Plants grow after 4 AM — ON (previously OFF)
- Botanists water after 4 AM — ON (previously OFF)
- Prevent water loss after 4 AM — ON (unchanged)

Existing installations retain their saved settings. Upgrading will **not** automatically re-enable an option that a player previously disabled.

The growth multiplier, native growth algorithm, task logic, and JSON configuration format are unchanged. Diagnostics stay off by default for fresh installs.

## Install and update

1. Install MelonLoader for Schedule I (tested with v0.7.3 Open-Beta).
2. Close the game.
3. Replace the existing `ScheduleI24Seven.dll` in the game's `Mods` folder with the newly built DLL once released.
4. Launch the game and check the settings through the optional Mod Manager & Phone App v2.2.4.

Existing settings are retained from `UserData/MelonPreferences.cfg` and `UserData/ScheduleI24Seven.json`. To enable previously disabled options, change them manually in the phone menu or JSON file.

## Compatibility and notes

- Tested previous release: Schedule I 0.4.7f12 / MelonLoader 0.7.3 Open-Beta.
- Very high positive growth multipliers can make plants mature instantly or skip visible growth stages.
- Botanists still require assigned pots and available watering supplies.
- Pot protection suppresses the entire frozen-time pot minute update, which may affect other pot minute behavior.
- Newer game versions and multiplayer require separate verification.

## Before release

- [ ] Pull development branch and build v0.5.1 successfully.
- [ ] Confirm the loader identifies v0.5.1.
- [ ] Verify the four defaults on a fresh settings profile, without replacing a user's actual settings.
- [ ] Verify existing settings persist across upgrade.
- [ ] Test work, watering, growth and moisture protection after 4 AM.
- [ ] Package and checksum the DLL ZIP.
- [ ] Obtain approval before merging, tagging or publishing to GitHub and Nexus Mods.

Copyright © 2026 AnasaAyatan. All rights reserved. See [LICENSE](LICENSE).
