<#
.SYNOPSIS
    Creates a 5-stage workflow in ezSaaSApi and runs the full instance flow (publish, start, move, approve, complete).

.DESCRIPTION
    End-to-end test for the modern workflow API (NOT legacy /api/workflow/transaction):

      1. Signup tenant (optional) + login
      2. POST /api/workflows                         - create workflow definition
      3. POST /api/workflows/{id}/steps (x5)         - add 5 definition steps
      4. POST /api/workflows/{id}/publish          - activate workflow
      5. POST /api/workflows/{id}/start            - create workflow instance
      6. POST .../instances/{id}/move-next           - insert step 1, review step 1, review step 2
      7. POST .../steps/{stepId}/approve (x3)      - approve stages 3-5
      8. GET  /api/workflows/{id}/instances/{id}   - verify Completed status

    Prerequisites: API running, catalog + tenant DB configured.

.EXAMPLE
    .\scripts\FiveStage-Workflow-SaaSApi.ps1

.EXAMPLE
    .\scripts\FiveStage-Workflow-SaaSApi.ps1 -BaseUrl "https://localhost:5001" -SkipSignup -TenantId "your-tenant-guid"

.EXAMPLE
    .\scripts\FiveStage-Workflow-SaaSApi.ps1 -RejectAtFinance
#>

param(
    [string]$BaseUrl = "https://localhost:5001",
    [string]$OrgName = "Five Stage Demo",
    [string]$Email = "demo@ezofis.com",
    [string]$Password = "Ezofis@123",
    [string]$FirstName = "Demo",
    [string]$LastName = "User",
    [switch]$SkipSignup,
    [string]$TenantId = "",
    [switch]$RejectAtFinance
)

$ErrorActionPreference = "Stop"

function Enable-LocalhostTlsBypass {
    param([string]$Url)
    if ($Url -notmatch "localhost") { return }

    if ($PSVersionTable.PSVersion.Major -ge 7) {
        $script:SkipCert = $true
        return
    }

    add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint s, X509Certificate c, WebRequest r, int p) { return true; }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
}

function Get-ApiErrorBody {
    param($Exception)
    if ($Exception.ErrorDetails.Message) { return $Exception.ErrorDetails.Message }
    if (-not $Exception.Response) { return $Exception.Message }
    try {
        $stream = $Exception.Response.GetResponseStream()
        if (-not $stream) { return $Exception.Message }
        $reader = New-Object System.IO.StreamReader($stream)
        $body = $reader.ReadToEnd()
        $reader.Close()
        return $body
    } catch {
        return $Exception.Message
    }
}

function Invoke-SaaSApi {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body,
        [hashtable]$ExtraHeaders = @{}
    )

    $headers = @{
        "Content-Type" = "application/json"
        "Accept"       = "application/json"
    }
    foreach ($k in $ExtraHeaders.Keys) { $headers[$k] = $ExtraHeaders[$k] }

    $params = @{
        Method        = $Method
        Uri           = $Uri
        Headers       = $headers
        ErrorAction   = "Stop"
    }
    if ($Body -ne $null) {
        # If Body is already a JSON document string, send it as-is (do not ConvertTo-Json — that wraps
        # the payload in quotes and causes "'0x0A' is invalid within a JSON string" on the API).
        if ($Body -is [string]) {
            $trim = $Body.TrimStart()
            if ($trim.StartsWith('{') -or $trim.StartsWith('[')) {
                $params.Body = $Body
            } else {
                $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress:$false)
            }
        } else {
            $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress:$false)
        }
    }
    if ($script:SkipCert) { $params.SkipCertificateCheck = $true }

    try {
        return Invoke-RestMethod @params
    } catch {
        $detail = Get-ApiErrorBody -Exception $_.Exception
        throw "$Method $Uri failed: $detail"
    }
}

function Get-Prop {
    param($Object, [string[]]$Names)
    foreach ($n in $Names) {
        if ($null -eq $Object) { continue }
        $p = $Object.PSObject.Properties[$n]
        if ($p) { return $p.Value }
    }
    return $null
}

function Get-StepInstanceIdByOrder {
    param($StepInstances, [int]$Order)
    if (-not $StepInstances) { return $null }
    $step = $StepInstances | Where-Object {
        (Get-Prop $_ @("order", "Order")) -eq $Order
    } | Select-Object -First 1
    if (-not $step) { $step = $StepInstances[$Order - 1] }
    return Get-Prop $step @("id", "Id")
}

function Get-DefinitionStepActivityId {
    param($DefinitionSteps, [int]$Order)
    if (-not $DefinitionSteps) { return $null }
    $step = $DefinitionSteps | Where-Object {
        (Get-Prop $_ @("order", "Order")) -eq $Order
    } | Select-Object -First 1
    if (-not $step) { return $null }
    $activityId = Get-Prop $step @("activityId", "ActivityId")
    if ($activityId) { return $activityId }
    return (Get-Prop $step @("id", "Id"))
}

function Get-InstanceStatusLabel {
    param($Status)
    switch ($Status) {
        0 { "Draft" }
        1 { "Running" }
        2 { "Paused" }
        3 { "Completed" }
        4 { "Failed" }
        5 { "Cancelled" }
        default { $Status }
    }
}

Enable-LocalhostTlsBypass -Url $BaseUrl

Write-Host ""
Write-Host "=== Five-Stage Workflow Full Flow (ezSaaSApi) ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host ""

# --- 1. Tenant ---
if ($SkipSignup -and $TenantId) {
    $tenantId = $TenantId
    Write-Host "[1/12] Using existing tenant: $tenantId" -ForegroundColor Cyan
} else {
    Write-Host "[1/12] Signup new tenant..." -ForegroundColor Cyan
    $suffix = Get-Random -Minimum 1000 -Maximum 9999
    $signup = Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/Signup" -Body @{
        organizationName = "$OrgName $suffix"
        email            = $Email
        password         = $Password
        firstName        = $FirstName
        lastName         = $LastName
    }
    $tenantId = Get-Prop $signup @("tenantId", "TenantId")
    if (-not $tenantId) { throw "Signup did not return tenantId." }
    Write-Host "       TenantId: $tenantId" -ForegroundColor Green
}

# --- 2. Login ---
Write-Host "[2/12] Login..." -ForegroundColor Cyan
$login = Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/auth/ezofis/login" -Body @{
    email    = $Email
    password = $Password
} -ExtraHeaders @{ "X-Tenant-Id" = $tenantId }

$token = Get-Prop $login @("accessToken", "AccessToken", "token", "Token")
if (-not $token) {
    if (Get-Prop $login @("tempToken", "TempToken")) {
        throw "2FA is enabled for this user. Complete 2FA login manually, then use -SkipSignup -TenantId."
    }
    throw "Login did not return accessToken."
}

$auth = @{
    "Authorization" = "Bearer $token"
    "X-Tenant-Id"   = $tenantId
}
Write-Host "       OK" -ForegroundColor Green

# --- 3. Create workflow definition ---
Write-Host "[3/12] Create workflow definition..." -ForegroundColor Cyan
$wfName = "Five Stage Flow $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
$create = Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows" -Body @{
    name        = $wfName
    description = "Automated 5-stage test: Submit -> Dept -> Finance -> Manager -> Final"
    triggerType = 0
} -ExtraHeaders $auth

$workflowId = Get-Prop $create @("workflowId", "WorkflowId")
Write-Host "       WorkflowId: $workflowId" -ForegroundColor Green

# --- 4. Add 5 definition steps (POST /api/workflows/{id}/steps) ---
Write-Host "[4/12] Add 5 workflow definition steps..." -ForegroundColor Cyan
$definitionSteps = @(
    @{ name = "Stage 1 - Submit";      stepType = 0; order = 1; description = "Submit request"; isRequired = $true }
    @{ name = "Stage 2 - Dept Review"; stepType = 0; order = 2; description = "Department review"; isRequired = $true }
    @{ name = "Stage 3 - Finance";     stepType = 1; order = 3; description = "Finance approval"; isRequired = $true }
    @{ name = "Stage 4 - Manager";     stepType = 1; order = 4; description = "Manager approval"; isRequired = $true }
    @{ name = "Stage 5 - Final";       stepType = 1; order = 5; description = "Final sign-off"; isRequired = $true }
)
foreach ($s in $definitionSteps) {
    Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/$workflowId/steps" -Body $s -ExtraHeaders $auth | Out-Null
}
Write-Host "       Added: $($definitionSteps.name -join ' -> ')" -ForegroundColor Green

# --- 5. Verify steps on GET workflow ---
$wfDetail = Invoke-SaaSApi -Method GET -Uri "$BaseUrl/api/workflows/$workflowId" -ExtraHeaders $auth
$stepCount = @(Get-Prop $wfDetail @("steps", "Steps")).Count
Write-Host "       GET workflow: $stepCount step(s), status=$(Get-Prop $wfDetail @('status','Status'))" -ForegroundColor Gray

# --- 6. Publish ---
Write-Host "[5/12] Publish workflow..." -ForegroundColor Cyan
Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/$workflowId/publish" -ExtraHeaders $auth | Out-Null
Write-Host "       OK (Active)" -ForegroundColor Green

# --- 7. Start instance ---
Write-Host "[6/12] Start workflow instance..." -ForegroundColor Cyan
$start = Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/$workflowId/start" -Body @{
    context = '{"referenceNumber":"REQ-5STAGE-001","amount":5000}'
} -ExtraHeaders $auth
$instanceId = Get-Prop $start @("instanceId", "InstanceId")
Write-Host "       InstanceId: $instanceId" -ForegroundColor Green

# --- 8. Comment ---
Write-Host "[7/12] Add instance comment..." -ForegroundColor Cyan
Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/comments" -Body @{
    comments = "Five-stage test started for $wfName"
} -ExtraHeaders $auth | Out-Null
Write-Host "       OK" -ForegroundColor Green

# --- 9-10. Move through manual stages 1-2 (activityId + optional review) ---
$definitionStepsList = @(Get-Prop $wfDetail @("steps", "Steps"))
$stage1ActivityId = Get-DefinitionStepActivityId -DefinitionSteps $definitionStepsList -Order 1
$stage2ActivityId = Get-DefinitionStepActivityId -DefinitionSteps $definitionStepsList -Order 2
if (-not $stage1ActivityId -or -not $stage2ActivityId) {
    throw "Could not resolve activityId for workflow definition steps 1-2."
}

Write-Host "[8/12] Move next: insert Stage 1 transaction row..." -ForegroundColor Cyan
Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/move-next" -Body @{
    activityId = $stage1ActivityId
    comments   = "Stage 1 submit - insert step"
} -ExtraHeaders $auth | Out-Null

Write-Host "[9/12] Move next: review Stage 1 -> open Stage 2..." -ForegroundColor Cyan
Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/move-next" -Body @{
    activityId = $stage1ActivityId
    review     = "Start"
    comments   = "Stage 1 submit complete"
} -ExtraHeaders $auth | Out-Null

Write-Host "[10/12] Move next: review Stage 2 -> Stage 3..." -ForegroundColor Cyan
Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/move-next" -Body @{
    activityId = $stage2ActivityId
    review     = "Dept Review"
    comments   = "Department review complete"
} -ExtraHeaders $auth | Out-Null

# --- 11. Approve or reject at finance (stage 3) ---
$instance = Invoke-SaaSApi -Method GET -Uri "$BaseUrl/api/workflows/$workflowId/instances/$instanceId" -ExtraHeaders $auth
$stepInstances = Get-Prop $instance @("stepInstances", "StepInstances")

if ($RejectAtFinance) {
    Write-Host "[11/12] Reject at Stage 3 (Finance)..." -ForegroundColor Cyan
    $financeStepId = Get-StepInstanceIdByOrder -StepInstances $stepInstances -Order 3
    Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/steps/$financeStepId/reject" -Body @{
        reason         = "Budget exceeded"
        cancelWorkflow = $true
    } -ExtraHeaders $auth | Out-Null
    Write-Host "       Workflow cancelled" -ForegroundColor Yellow
} else {
    Write-Host "[11/12] Approve Stage 3 (Finance)..." -ForegroundColor Cyan
    $financeStepId = Get-StepInstanceIdByOrder -StepInstances $stepInstances -Order 3
    Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/steps/$financeStepId/approve" -Body @{
        comments       = "Finance approved"
        moveToNextStep = $true
    } -ExtraHeaders $auth | Out-Null

    Write-Host "[12/12] Approve Stage 4 (Manager)..." -ForegroundColor Cyan
    $instance = Invoke-SaaSApi -Method GET -Uri "$BaseUrl/api/workflows/$workflowId/instances/$instanceId" -ExtraHeaders $auth
    $stepInstances = Get-Prop $instance @("stepInstances", "StepInstances")
    $managerStepId = Get-StepInstanceIdByOrder -StepInstances $stepInstances -Order 4
    Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/steps/$managerStepId/approve" -Body @{
        comments       = "Manager approved"
        moveToNextStep = $true
    } -ExtraHeaders $auth | Out-Null

    Write-Host "[13/12] Approve Stage 5 (Final)..." -ForegroundColor Cyan
    $instance = Invoke-SaaSApi -Method GET -Uri "$BaseUrl/api/workflows/$workflowId/instances/$instanceId" -ExtraHeaders $auth
    $stepInstances = Get-Prop $instance @("stepInstances", "StepInstances")
    $finalStepId = Get-StepInstanceIdByOrder -StepInstances $stepInstances -Order 5
    Invoke-SaaSApi -Method POST -Uri "$BaseUrl/api/workflows/instances/$instanceId/steps/$finalStepId/approve" -Body @{
        comments       = "Final approval - workflow complete"
        moveToNextStep = $true
    } -ExtraHeaders $auth | Out-Null
}

# --- Verify ---
Write-Host ""
Write-Host "=== Verification ===" -ForegroundColor Cyan
$final = Invoke-SaaSApi -Method GET -Uri "$BaseUrl/api/workflows/$workflowId/instances/$instanceId" -ExtraHeaders $auth
$finalStatus = Get-Prop $final @("status", "Status")
$expected = if ($RejectAtFinance) { 5 } else { 3 }
$statusLabel = Get-InstanceStatusLabel -Status $finalStatus

Write-Host "Instance status: $statusLabel ($finalStatus)" -ForegroundColor $(if ($finalStatus -eq $expected) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "Step instances:" -ForegroundColor Gray
foreach ($si in (Get-Prop $final @("stepInstances", "StepInstances"))) {
    $o = Get-Prop $si @("order", "Order")
    $n = Get-Prop $si @("stepName", "StepName")
    $st = Get-Prop $si @("status", "Status")
    Write-Host "  Order $o : $n -> $(Get-InstanceStatusLabel -Status $st)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "TenantId:   $tenantId"
Write-Host "WorkflowId: $workflowId"
Write-Host "InstanceId: $instanceId"
Write-Host "GET URL:    $BaseUrl/api/workflows/$workflowId/instances/$instanceId"
Write-Host ""
Write-Host "Done." -ForegroundColor Green
