# Schedule I 24/7 — Stable Release Draft

> **Draft only — not published.** Candidate: `v0.4.11-alpha` from `development/frozen-growth-watering`. Assign the stable version and publish only after the final build and in-game checks pass.

## Keep working after 4 AM

Schedule I 24/7 keeps supported employees working and plants growing through the game's end-of-day freeze.

### Highlights

- Employees can continue working after 4 AM.
- Assigned botanists can water plants through their normal in-game behavior.
- Plants can continue growing with an adjustable, uncapped positive growth multiplier.
- Pot moisture is protected against frozen-time minute-based draining.
- All settings are editable inside the compatible in-game phone Mod Manager.
- Phone settings synchronize with `UserData/ScheduleI24Seven.json`.
- Payroll and wage amounts are left to the game.

### Growth multiplier warning

**Very high multipliers can cause plants to mature almost instantly and skip visible growth stages.** The multiplier has no artificial upper cap, but plant progress remains within 0–100%.

## Installation

1. Install MelonLoader 0.7.3 Open-Beta for Schedule I (the tested loader version).
2. Close the game.
3. Download the compiled `ScheduleI24Seven.dll` release asset **once published**, or build it from source.
4. Copy the DLL into `Schedule I/Mods/`.
5. Launch the game, then optionally open Phone → Mod Manager → Schedule I 24/7 to configure it.

The phone UI was tested with Mod Manager & Phone App 2.2.4; the mod also supports direct JSON configuration.

## Compatibility

- Tested game version: Schedule I v0.4.7f12.
- Tested loader: MelonLoader v0.7.3 Open-Beta.
- Supported phone UI tested with Mod Manager & Phone App v2.2.4.
- Newer game versions and multiplayer require separate compatibility checks.

## Important limitations

- The mod suppresses `Pot.OnMinPass()` during the freeze when water protection is enabled; other pot-minute logic may also pause.
- During the frozen period, employee eligibility checks may be broader than simply checking the time.
- Bots need valid pot assignments and ordinary watering supplies; the mod does not generate water.
- Growth can jump to completion at extreme values. Back up saves before experimenting.
- This is not an official game modification.

## Final release checklist

- [ ] Pull the latest `development/frozen-growth-watering` commits.
- [ ] Build Release configuration successfully with no compile errors.
- [ ] Confirm the installed mod version in MelonLoader and phone UI.
- [ ] Confirm new growth-multiplier warning is visible in the phone settings.
- [ ] Confirm settings save and persist after restarting.
- [ ] Confirm employees and botanists continue working after 4 AM.
- [ ] Confirm plant growth and moisture behavior after 4 AM, at normal and higher multipliers.
- [ ] Confirm normal payroll behavior and no new game errors.
- [ ] Decide the stable version, update assembly and documentation consistently.
- [ ] Merge the tested code into `main` **only after explicit approval**.
- [ ] Attach a release-built DLL to GitHub Releases; verify its SHA-256 before announcing the download.

## Credits and rights

Developed by AnasaAyatan. Copyright © 2026 AnasaAyatan. All rights reserved. No game binaries included in the source repository.
