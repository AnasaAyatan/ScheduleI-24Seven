param(
    [string]$SourceFile = (Join-Path $PSScriptRoot '../src/Mod.cs')
)
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath $SourceFile -Raw

$required = [ordered]@{
    'Version 0.6.0' = '"0.6.0", "AnasaAyatan"'
    'Automatic jobs off by default' = 'CreateEntry("EnableAutoLaunderTransactions", false,'
    'Job cap defaults to available capacity' = 'CreateEntry("AutoLaunderBusinessCap", 0f,'
    'Per-job cap phone label' = '"Max per automatic job ($)"'
    'Native UI confirmation' = 'confirm.Invoke(ui, null);'
    'Preserve manual terminal selection' = 'object? previousBusiness = businessProperty.GetValue(ui);'
    'Restore manual terminal selection' = 'initialize.Invoke(ui, new object[] { previousBusiness });'
    'Native amount validation' = 'actualSelected - requestAmount'
    'Business selection validation' = 'actualBusiness.Equals(business)'
    'Cash delta verification' = 'bool cashMatched ='
    'Operation count verification' = 'observedCount > _pendingOperationCount'
    'Uncertain-request halt' = '_autoStartHalted = true;'
    'Single-request scheduler' = 'StartAutoLaundering(business, amount);'
    'Fair scheduler rotation' = '_nextBusinessIndex = (businessIndex + 1)'
    'Pending request guard' = '_pendingAutoBusiness != null ||'
    'Minimum amount filter' = 'amount < MinimumAutomaticJobAmount()'
    'Cash reserve preference' = 'CreateEntry("MinimumCashReserve", 0f,'
    'Reserve-aware allocation' = 'cash - reserve'
    'Reserve-aware preflight' = 'cash - reserve < amount'
    'Business list load stabilization' = 'businesses.Length != _lastObservedOwnedBusinessCount'
    'Startup grace period' = 'now - _ownedBusinessesFirstObservedAt >= 60'
    'Whole-dollar transaction floor' = 'Math.Floor(Math.Min(Math.Max(0f, cash - reserve), effectiveRoom))'
    'One dollar fallback' = 'const float verifiedFallback = 1f;'
    'Normal time default' = 'advanced.CreateEntry("RealSecondsPerGameMinute", 1f,'
    'Technical plant interval in Advanced' = 'advanced.CreateEntry("SecondsPerPlantMinute", 60f,'
    'Technical plant multiplier in Advanced' = 'advanced.CreateEntry("AfterHoursGrowthMultiplier", 1.05f,'
    'Technical fallback rate in Advanced' = 'advanced.CreateEntry("NormalizedGrowthPerMinute", 0.01f,'
    'Cash reserve label' = '"Cash to keep ($)"'
    'Ten-second pacing' = '_nextAutoStart = now + 10;'
    'After-hours operation advancement' = 'AdvanceLaundering(elapsed, frozen);'
}
$failed = @()
foreach ($item in $required.GetEnumerator()) {
    if ($source.Contains($item.Value)) {
        Write-Host "[PASS] $($item.Key)"
    } else {
        Write-Error "[FAIL] $($item.Key): expected source invariant not found" -ErrorAction Continue
        $failed += $item.Key
    }
}
if ($source.Contains('CreateEntry("AutoLaunderFromInventory"')) {
    $failed += 'Obsolete capacity toggle must not be shown'
    Write-Error '[FAIL] Obsolete capacity toggle is still registered' -ErrorAction Continue
} else {
    Write-Host '[PASS] Capacity checking is mandatory, with no redundant toggle'
}
if ($failed.Count -gt 0) {
    throw "Source invariant validation failed: $($failed -join ', ')"
}
Write-Host "All source invariants present. Runtime behavior still requires in-game tests."
