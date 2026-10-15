param([Parameter(Mandatory=$true)][string]$RepoUrl)
$ErrorActionPreference = 'Stop'
git init
git branch -M main
git add .
git commit -m "Initial Schedule I 24/7 IL2CPP compatibility prototype"
git remote add origin $RepoUrl
git push -u origin main
