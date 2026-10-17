# Development workflow

## Branches

- `main` — published, stable code only. **Never push experimental laundering work directly to main.**
- `develop` — integration branch for unreleased v0.5.1 features; may be unstable.
- `feature/auto-laundering` — active test-driven development for automatic laundering, including native UI tracing and capped dry-run allocation.
- `development/frozen-growth-watering` — historical v0.5.1 working branch retained for reference. Do not delete until all work is migrated and confirmed.
- `development/24-seven-runtime` — legacy branch, retained for reference; behind `main`.

Future work: create `feature/<topic>` from `develop`; for small fixes use `fix/<topic>`; for urgent published fixes use `hotfix/<topic>` from `main`. Release preparation, only when explicitly approved, may use `release/vX.Y.Z`.

## TDD cycle

1. Create or switch to the relevant `feature/*` branch.
2. Implement one focused change with a specific observable pass/fail condition.
3. Commit and push; share the short SHA.
4. Build locally: `dotnet build -c Release` (Windows with installed game dependencies).
5. Back up a save before testing any game-state changes; install the compiled DLL and run the narrow test.
6. Capture the newest MelonLoader log. Fix failures on the **same feature branch**.
7. When the feature is working and reviewed, propose a PR into `develop`. Do not merge without approval.
8. Promote tested work from `develop` into `main` **only after explicit release approval**.

## Laundering safety requirements

- Experimental automatic paid transactions are disabled. Older builds could deduct cash without starting an operation.
- Read-only logging and dry-run planning may be tested without spending money.
- Before enabling transactions, verify the exact native manual request path, selected business, amount, account and operation creation.
- Never combine a direct cash debit and an unverified business RPC.
- Fail closed on missing APIs, uncertainty, duplicate/pending requests, or inconsistent balances.
- First paid test must be small and use a backed-up save; require confirmed operation creation before additional submissions.
- User-facing goal: mod-menu toggle to launder available carried cash automatically across eligible owned businesses, with `0` for full capacity or a positive per-business cap, then restart after completion.

## Windows test commands

```powershell
cd "C:\Users\user\Documents\ScheduleI-24Seven"
git switch feature/auto-laundering
git pull --ff-only origin feature/auto-laundering
dotnet build -c Release
```

Close the game before copying `bin\Release\net6.0\ScheduleI24Seven.dll` into its `Mods` folder.

## PR checklist

- [ ] Feature branch and target branch correct
- [ ] Changes have explicit pass/fail test
- [ ] Local build successful
- [ ] Game version and loader compatibility recorded
- [ ] Relevant MelonLoader log reviewed
- [ ] Cash/inventory/save safety checked
- [ ] README/changelog updated if user-visible behavior changed
- [ ] No merge to `main` without explicit release authorization
