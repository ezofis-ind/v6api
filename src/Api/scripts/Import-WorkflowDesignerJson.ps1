<#
.SYNOPSIS
    POST a v5 designer workflow JSON file to POST /api/workflows (raw body, not /steps).

.EXAMPLE
    .\scripts\Import-WorkflowDesignerJson.ps1 -JsonPath "$env:USERPROFILE\Downloads\c7ca0851761845bda19bc46404795f77.json" -TenantId "your-tenant-guid"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$JsonPath,
    [string]$BaseUrl = "https://localhost:5001",
    [Parameter(Mandatory = $true)]
    [string]$TenantId,
    [string]$Email = "demo@ezofis.com",
    [string]$Password = "Ezofis@123"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $JsonPath)) {
    throw "File not found: $JsonPath"
}

# Login
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/auth/login" -Body $loginBody -ContentType "application/json"
$token = $login.accessToken
if (-not $token) { throw "Login failed: no accessToken." }

$headers = @{
    Authorization  = "Bearer $token"
    "X-Tenant-Id"  = $TenantId
}

# Send designer JSON as raw application/json (NOT ConvertTo-Json on the file)
$rawJson = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $JsonPath))

try {
    $resp = Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/workflows" -Headers $headers -Body $rawJson -ContentType "application/json; charset=utf-8"
    Write-Host "Created workflow:" (Get-Member -InputObject $resp -Name workflowId,WorkflowId | ForEach-Object { $resp.($_.Name) }) -ForegroundColor Green
    $resp | ConvertTo-Json -Depth 5
} catch {
    if ($_.ErrorDetails.Message) { throw $_.ErrorDetails.Message }
    throw $_
}
