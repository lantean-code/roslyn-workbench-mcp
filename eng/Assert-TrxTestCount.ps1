[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Path,

    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $Minimum
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resultFiles = @(Get-ChildItem -Path $Path -Filter '*.trx' -Recurse -File)
if ($resultFiles.Count -eq 0) {
    throw "No TRX results were found under '$Path'."
}

$total = 0
foreach ($resultFile in $resultFiles) {
    [xml] $result = Get-Content -Path $resultFile.FullName -Raw
    $counters = $result.TestRun.ResultSummary.Counters
    if ($null -eq $counters) {
        throw "TRX result '$($resultFile.FullName)' does not contain result counters."
    }

    $total += [int] $counters.total
}

if ($total -lt $Minimum) {
    throw "Expected at least $Minimum tests under '$Path', but the TRX results contain $total."
}

Write-Host "Verified $total tests under '$Path' (minimum: $Minimum)."
