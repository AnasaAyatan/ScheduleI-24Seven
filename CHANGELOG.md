# Changelog

All notable user-facing changes to Schedule I 24/7 are documented here.

This repository uses a development branch for unreleased work. Versions and dates below describe the tested or candidate builds, not a published stable release.

## [Unreleased] — stable release preparation

- Prepared release documentation and a verification checklist.
- No changes to the tested gameplay mechanics in this documentation update.
- Stable version number, published release date, and binary download are **not yet assigned**.

## [0.4.11-alpha] — release candidate (tested in game)

### Added
- Clearer in-game descriptions for growth, employees, watering, and diagnostics.
- A warning that very high growth multipliers may instantly mature plants or skip visible stages.
- End-user installation and configuration guide.

### Changed
- Removed the artificial 300 simulated-minutes-per-update growth limit.
- Removed the artificial 0.05 fallback-growth cap.
- Removed the warning threshold for multipliers above 2×; multipliers remain uncapped.
- Removed hidden minimum clamps for positive JSON timing and growth-multiplier values.
- Kept normalized plant progress within its valid 0–100% range.
- Disabled detailed diagnostic logs by default for new installs; existing saved preferences remain in effect.

### Verification
- User reported the 0.4.11-alpha build working in game.
- The later description-only warning change has not yet been separately verified after rebuilding.

## [0.4.10-alpha] — botanist watering

### Added
- After-hours botanist watering behavior using the game's assigned-pot and watering APIs.
- Server-authority check and native work-in-progress check to avoid interrupting other tasks.

### Verified
- User reported botanist watering working after 4 AM.

## [0.4.7-alpha] — phone settings

- Made settings discoverable by Mod Manager & Phone App 2.2.4.
- Added in-game toggles and numeric fields.
- Synchronized settings between MelonPreferences and the JSON mirror.

## Earlier development

- Continued employee work during the end-of-day freeze.
- Added frozen-time plant growth and protection against pot minute-based moisture drain.
- Added plant-specific growth calculation and adjustable growth speed.

## Known limitations

- Growth rates rely on the game version's IL2CPP interfaces, with a fallback when native-rate calculation is unavailable.
- The water-protection hook suppresses the whole pot minute update after 4 AM; other pot-minute behaviors may also pause.
- `Employee.CanWork()` may bypass non-time restrictions during the 4 AM freeze.
- Multiplayer scenarios and newer game versions need separate testing.
- The mod does not change wages or automatically fill pots.
