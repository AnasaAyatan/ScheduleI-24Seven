# Contributing and development

Schedule I 24/7 is **source-available** under the terms in [LICENSE](LICENSE), not an open-source project. Review the license before modifying or redistributing any material. Contributions require maintainer approval.

## Workflow

- `main` holds published code. Create a separate `feature/<topic>` or `fix/<topic>` branch for new work.
- Keep changes focused; include a reproducible test or clear manual verification plan.
- Run `./tests/Verify-AutoLaundering.ps1` in PowerShell and `dotnet build -c Release`.
- Test game-state features on a backed-up save. Record game version, loader version and relevant MelonLoader log output.
- Open a pull request for review before merging. Do not upload personal save data, backups, local configuration or logs to GitHub.
- Do not change published tags or distribute altered binaries without permission under the project license.

## Automatic laundering safety

Automatic transactions must remain opt-in, respect native business capacity and carried-cash reserve, and verify both cash debit and operation creation. Never issue an unverified second transaction or combine independent manual cash debits with guessed network calls. Preserve the player's manual terminal state; halt further automatic submissions when the result is uncertain.

## Build

```powershell
git clone https://github.com/AnasaAyatan/ScheduleI-24Seven.git
cd ScheduleI-24Seven
.\tests\Verify-AutoLaundering.ps1
dotnet build -c Release
```

Output: `bin/Release/net6.0/ScheduleI24Seven.dll`. Close Schedule I before installing the DLL. See [README](README.md) for game requirements.
