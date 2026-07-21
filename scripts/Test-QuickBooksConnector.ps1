<#
.SYNOPSIS
  QuickBooks connector smoke test for V6 API.

.DESCRIPTION
  1. Optional SQL checks (catalog QUICKBOOKS row + tenant connector)
  2. Login (email/password + X-Tenant-Id)
  3. GET connector status, quickbooks masters/documents, master/resolve

.PARAMETER ApiBase
  API root without trailing slash, e.g. http://localhost:5000

.PARAMETER Email
  Ezofis login email

.PARAMETER Password
  Ezofis login password

.PARAMETER TenantId
  Tenant GUID (X-Tenant-Id)

.PARAMETER ConnectorId
  Existing QuickBooks connector GUID. If omitted, lists connectors and picks first QUICKBOOKS Connected.

.PARAMETER SqlServer
  Optional SQL Server for catalog/tenant checks (e.g. EZOFIS_DELL_I9)

.PARAMETER SqlUser
.PARAMETER SqlPassword
.PARAMETER CatalogDatabase
.PARAMETER TenantDatabase

.EXAMPLE
  .\scripts\Test-QuickBooksConnector.ps1 `
    -ApiBase http://localhost:5000 `
    -Email arasu@ezofis.com `
    -Password '***' `
    -TenantId 0B3E1B77-4A6C-46F2-83EE-2F0A5B84956B
#>
[CmdletBinding()]
param(
    [string] $ApiBase = "http://localhost:5000",
    [Parameter(Mandatory = $true)]
    [string] $Email,
    [Parameter(Mandatory = $true)]
    [string] $Password,
    [Parameter(Mandatory = $true)]
    [Guid] $TenantId,
    [Guid] $ConnectorId = [Guid]::Empty,
    [string] $SqlServer = "",
    [string] $SqlUser = "sa",
    [string] $SqlPassword = "",
    [string] $CatalogDatabase = "Ezofis_catalog_new",
    [string] $TenantDatabase = ""
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg) { Write-Host "OK  $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "WARN $msg" -ForegroundColor Yellow }
function Write-Fail($msg) { Write-Host "FAIL $msg" -ForegroundColor Red }

if ($SqlServer -and $SqlPassword) {
    Write-Step "SQL catalog check (QUICKBOOKS provider)"
    $catalogQ = @"
SELECT ProviderCode, DisplayName,
  CASE WHEN LEN(ClientId) > 0 THEN 'yes' ELSE 'no' END AS HasClientId,
  CASE WHEN LEN(ClientSecret) > 0 THEN 'yes' ELSE 'no' END AS HasSecret,
  ISNULL(NULLIF(RedirectUri, ''), '(empty)') AS RedirectUri
FROM catalog.ConnectorProviders WHERE ProviderCode = N'QUICKBOOKS';
"@
    sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -C -d $CatalogDatabase -Q $catalogQ -W -s "|"
    if ($LASTEXITCODE -ne 0) { Write-Warn "Catalog SQL check failed" }

    if (-not $TenantDatabase) {
        $TenantDatabase = "ezofis_Tenant_$($TenantId.ToString('N').ToUpper())"
    }
    Write-Step "SQL tenant connector check ($TenantDatabase)"
    $tenantQ = @"
SELECT Id, Name, ProviderCode, OAuthStatus, ExternalAccountEmail,
  ISNULL(ExternalAccountId, '(null)') AS RealmId,
  CASE WHEN AccessToken IS NULL OR AccessToken = '' THEN 'no' ELSE 'yes' END AS HasToken
FROM dbo.connector
WHERE IsDeleted = 0 AND ProviderCode = N'QUICKBOOKS';
"@
    sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -C -d $TenantDatabase -Q $tenantQ -W -s "|"
}

Write-Step "Health check $ApiBase/swagger"
try {
    $swagger = Invoke-WebRequest -Uri "$ApiBase/swagger/index.html" -UseBasicParsing -TimeoutSec 10
    Write-Ok "Swagger HTTP $($swagger.StatusCode)"
} catch {
    Write-Fail "API not reachable at $ApiBase — start with: dotnet run --project src/Api"
    exit 1
}

Write-Step "Login"
$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
$login = Invoke-RestMethod -Method POST -Uri "$ApiBase/api/auth/ezofis/login" `
    -Headers @{ "X-Tenant-Id" = $TenantId.ToString() } `
    -ContentType "application/json" -Body $loginBody

if ($login.tempToken) {
    Write-Fail "2FA enabled — complete POST /api/auth/2fa/complete with tempToken first."
    exit 1
}
$token = $login.accessToken
if (-not $token) {
    Write-Fail "No accessToken in login response."
    exit 1
}
Write-Ok "Logged in as $Email"

$headers = @{
    Authorization  = "Bearer $token"
    "X-Tenant-Id"  = $TenantId.ToString()
}

if ($ConnectorId -eq [Guid]::Empty) {
    Write-Step "List connectors — find QUICKBOOKS"
    $all = Invoke-RestMethod -Method GET -Uri "$ApiBase/api/connector/all" -Headers $headers
    $qb = @($all | Where-Object { $_.providerCode -eq "QUICKBOOKS" -or $_.ProviderCode -eq "QUICKBOOKS" })
    if ($qb.Count -eq 0) {
        Write-Fail "No QUICKBOOKS connector. Authorize first:"
        Write-Host @'

POST /api/connector/oauth/authorize
{
  "providerCode": "QUICKBOOKS",
  "name": "QuickBooks",
  "configJson": "{\"environment\":\"sandbox\"}",
  "successRedirectUrl": "http://localhost:4200/connectors/oauth-complete"
}

'@ -ForegroundColor Yellow
        exit 1
    }
    $connected = $qb | Where-Object { $_.oauthStatus -eq "Connected" -or $_.OAuthStatus -eq "Connected" } | Select-Object -First 1
    $pick = if ($connected) { $connected } else { $qb[0] }
    $pickId = if ($pick.id) { $pick.id } else { $pick.Id }
    $pickName = if ($pick.name) { $pick.name } else { $pick.Name }
    $ConnectorId = [Guid]$pickId
    Write-Ok "Using connector $ConnectorId ($pickName)"
}

$cid = $ConnectorId.ToString()

Write-Step "GET /api/connector/$cid/status"
$status = Invoke-RestMethod -Method GET -Uri "$ApiBase/api/connector/$cid/status" -Headers $headers
$status | ConvertTo-Json -Depth 5
if (-not $status.isConnected) {
    Write-Fail "Connector not connected — complete OAuth authorize flow."
    exit 1
}
Write-Ok "Connected ($($status.externalAccountEmail))"

Write-Step "GET quickbooks/masters?masterType=Vendor"
$masters = Invoke-RestMethod -Method GET -Uri "$ApiBase/api/connector/$cid/quickbooks/masters?masterType=Vendor&maxResults=5" -Headers $headers
Write-Ok "Vendors returned: $($masters.items.Count)"

Write-Step "GET quickbooks/documents?documentType=Invoice"
$docs = Invoke-RestMethod -Method GET -Uri "$ApiBase/api/connector/$cid/quickbooks/documents?documentType=Invoice&maxResults=5" -Headers $headers
Write-Ok "Invoices returned: $($docs.items.Count)"

Write-Step "GET /api/master/resolve (QuickBooks Vendor)"
$resolve = Invoke-RestMethod -Method GET `
    -Uri "$ApiBase/api/master/resolve?type=Vendor&source=QuickBooks&connectorId=$cid&maxResults=5" `
    -Headers $headers
Write-Ok "Resolve items: $($resolve.items.Count)"
$resolve | ConvertTo-Json -Depth 4

Write-Host "`nAll QuickBooks smoke checks passed." -ForegroundColor Green
