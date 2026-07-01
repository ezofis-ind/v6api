<#
.SYNOPSIS
  Reliable Release publish for SaaSApp.Api (fixes partial DLL copy / IIS lock issues).

.EXAMPLE
  .\scripts\Publish-V6Api.ps1
  .\scripts\Publish-V6Api.ps1 -OutputPath "D:\Aravinthan_Backup\V6_SAAS_API" -AppPoolName "YourPool"
#>
param(
    [string]$OutputPath = "D:\Aravinthan_Backup\V6_SAAS_API",
    [string]$AppPoolName = "",
    [string]$ProjectPath = "$PSScriptRoot\..\src\Api\SaaSApp.Api.csproj"
)

$ErrorActionPreference = "Stop"
$ProjectPath = (Resolve-Path $ProjectPath).Path

function Stop-PoolIfRequested {
    if ([string]::IsNullOrWhiteSpace($AppPoolName)) { return }
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module WebAdministration) {
        if ((Get-WebAppPoolState -Name $AppPoolName).Value -ne "Stopped") {
            Write-Host "Stopping app pool '$AppPoolName'..." -ForegroundColor Yellow
            Stop-WebAppPool -Name $AppPoolName
            Start-Sleep -Seconds 3
        }
    }
}

function Start-PoolIfRequested {
    if ([string]::IsNullOrWhiteSpace($AppPoolName)) { return }
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module WebAdministration) {
        Write-Host "Starting app pool '$AppPoolName'..." -ForegroundColor Green
        Start-WebAppPool -Name $AppPoolName
    }
}

# Stop IIS worker processes that may still lock DLLs after app pool stop.
Get-Process w3wp -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping w3wp (PID $($_.Id))..." -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force
}

Stop-PoolIfRequested

# app_offline.htm releases IIS file locks during deploy
$offline = Join-Path $OutputPath "app_offline.htm"
$hadOffline = $false
if (Test-Path $OutputPath) {
    @"
<!DOCTYPE html><html><body><h1>Deploying V6 API — back shortly.</h1></body></html>
"@ | Set-Content -Path $offline -Encoding UTF8
    $hadOffline = $true
    Start-Sleep -Seconds 2
}

Write-Host "Cleaning Release build..." -ForegroundColor Cyan
dotnet clean $ProjectPath -c Release -v q

Write-Host "Publishing to $OutputPath ..." -ForegroundColor Cyan
dotnet publish $ProjectPath -c Release -o $OutputPath

if ($hadOffline -and (Test-Path $offline)) {
    Remove-Item $offline -Force
}

$releaseBin = Join-Path (Split-Path $ProjectPath -Parent) "bin\Release\net8.0"
$checkFiles = @(
    "SaaSApp.Api.dll",
    "SaaSApp.Workflow.Infrastructure.dll",
    "SaaSApp.Repository.Infrastructure.dll"
)

Write-Host "`nVerify timestamps (publish folder vs Release bin):" -ForegroundColor Cyan
$failed = $false
foreach ($name in $checkFiles) {
    $pub = Get-Item (Join-Path $OutputPath $name) -ErrorAction SilentlyContinue
    $bin = Get-Item (Join-Path $releaseBin $name) -ErrorAction SilentlyContinue
    if (-not $pub -or -not $bin) {
        Write-Host "  MISSING: $name" -ForegroundColor Red
        $failed = $true
        continue
    }
    $match = ($pub.LastWriteTime -eq $bin.LastWriteTime) -and ($pub.Length -eq $bin.Length)
    $color = if ($match) { "Green" } else { "Red" }
    Write-Host ("  {0,-42} pub={1} bin={2} {3}" -f $name, $pub.LastWriteTime, $bin.LastWriteTime, $(if ($match) { "OK" } else { "MISMATCH" })) -ForegroundColor $color
    if (-not $match) { $failed = $true }
}

Start-PoolIfRequested

if ($failed) {
    Write-Host "`nPublish folder is OUT OF SYNC. Stop Visual Studio debug (F5), stop all dotnet hosts, then re-run." -ForegroundColor Red
    exit 1
}

Write-Host "`nPublish completed successfully." -ForegroundColor Green
