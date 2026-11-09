# Schedule I 24/7 v0.6.1 — Plant-growth stability hotfix

**Status:** Release candidate; not yet published. Stable published version remains [v0.6.0](https://github.com/AnasaAyatan/ScheduleI-24Seven/releases/tag/v0.6.0).

## Changes

- Extended the valid lifetime of a positive end-of-day observation from 3 seconds to 15 seconds, reducing short interruptions in after-hours operations when the game stops polling its getter.
- Added detailed plant-growth diagnostic counts (found, advanced, fully grown, and no-growth) to distinguish a missing plant from an already mature plant.
- No changes to the existing laundering, botanist, employee, JSON configuration or planned v0.7.0 logistics features.

## Tested

- Schedule I 0.4.7f12; MelonLoader 0.7.3 Open-Beta.
- November 8 diagnostic log confirms after-hours plant-progress updates; end-of-day state remained valid past the previous 3-second timeout.
- The test used accelerated simulation speed. Normal-speed behaviour, moisture preservation, multiplayer and other game versions are not independently confirmed.

## Known limitations

- A 15-second cache expiration is a mitigation, not guaranteed game-clock detection.
- The independent external AutoRestock mod produced a storage-visualizer exception in the same test session; it is not fixed by this hotfix.
- High simulation speeds can skip growth stages.

## Installation (after release)

Build from this branch or obtain the compiled `ScheduleI24Seven.dll` from the eventual v0.6.1 GitHub release asset. Replace the previous DLL in the game's `Mods` directory while the game is closed, after backing up the old DLL. Do not install GitHub auto-generated source archives as a mod binary.

© 2026 AnasaAyatan. See [LICENSE](LICENSE).
