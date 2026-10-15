param(
    [ValidateSet('main', 'development/24-seven-runtime')]
    [string]$Branch = 'development/24-seven-runtime',
    [switch]$Push
)

$ErrorActionPreference = 'Stop'
git fetch origin
if ($LASTEXITCODE -ne 0) { throw 'Git fetch failed.' }

git switch $Branch
if ($LASTEXITCODE -ne 0) { throw 'Could not switch branches.' }

git pull --ff-only origin $Branch
if ($LASTEXITCODE -ne 0) { throw 'Pull failed. Resolve local changes before proceeding.' }

if ($Push) {
    git push origin $Branch
    if ($LASTEXITCODE -ne 0) { throw 'Push failed.' }
}

Write-Host "Repository synchronized on $Branch"
Write-Host 'Wages are controlled only by the unmodified game at normal day transitions.'
