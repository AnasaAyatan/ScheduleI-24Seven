param(
    [string]$SourceFile = (Join-Path $PSScriptRoot '../src/Mod.cs')
)
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath $SourceFile -Raw

$required = [ordered]@{
    'Native UI confirmation' = 'confirm.Invoke(ui, null);'
    'Native amount validation' = 'actualSelected - requestAmount'
    'Business selection validation' = 'actualBusiness.Equals(business)'
    'Cash delta verification' = 'bool cashMatched ='
    'Operation count verification' = 'observedCount > _pendingOperationCount'
    'Uncertain-request halt' = '_autoStartHalted = true;'
    'Single-request scheduler' = 'StartAutoLaundering(business, amount);'
    'Fair scheduler rotation' = '_nextBusinessIndex = (businessIndex + 1)'
    'Pending request guard' = '_pendingAutoBusiness != null ||'
    'Minimum amount filter' = 'amount < MinimumAutomaticJobAmount()'
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
if ($failed.Count -gt 0) {
    throw "Source invariant validation failed: $($failed -join ', ')"
}
Write-Host "All source invariants present. Runtime behavior still requires in-game tests."
