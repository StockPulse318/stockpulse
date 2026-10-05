<#
.SYNOPSIS
    Convenience wrapper to seed the local StockPulse backend instance.
.EXAMPLE
    .\seed-local.ps1
    .\seed-local.ps1 -Port 5242
#>

param(
    [int]$Port = 0,
    [string]$Username = "admin",
    [string]$Password = "Admin@1234"
)

$targetUrl = ""
if ($Port -gt 0) {
    $targetUrl = "http://localhost:$Port"
}

$scriptPath = Join-Path $PSScriptRoot "seed-remote-api.ps1"
if (-not (Test-Path $scriptPath)) {
    Write-Error "Could not locate seed-remote-api.ps1 in $PSScriptRoot"
    exit 1
}

if ($targetUrl) {
    & $scriptPath -BaseUrl $targetUrl -Username $Username -Password $Password
} else {
    & $scriptPath -Local -Username $Username -Password $Password
}
