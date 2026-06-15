# E2E All Workflow Types Test
# Runs both: Invoice Approval (5 steps) + Multi-User Approval Policy (AnyOneApprove, AllMustApprove)
# Prerequisites: API running, Catalog DB set up
#
# Usage: .\Test-E2EAllWorkflows.ps1
#        .\Test-E2EAllWorkflows.ps1 -BaseUrl "https://localhost:5001"
#        .\Test-E2EAllWorkflows.ps1 -SkipWorkflow1 -SkipWorkflow2  # Run only one

param(
    [string]$BaseUrl = "https://localhost:5001",
    [switch]$SkipWorkflow1,
    [switch]$SkipWorkflow2
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host "`n=== E2E All Workflow Types Test ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Yellow
Write-Host ""

$failed = 0

# Test 1: Invoice Approval (5 steps) - complete or reject
if (-not $SkipWorkflow1) {
    Write-Host "--- Test 1: Invoice Approval (5 steps) ---" -ForegroundColor Magenta
    try {
        & "$scriptDir\Test-E2EWorkflow.ps1" -BaseUrl $BaseUrl
        Write-Host "  OK: Invoice Approval test passed" -ForegroundColor Green
    } catch {
        Write-Host "  FAIL: $_" -ForegroundColor Red
        $failed++
    }
    Write-Host ""
} else {
    Write-Host "Skipping Test 1 (Invoice Approval)" -ForegroundColor Gray
}

# Test 2: Multi-User Approval Policy (AnyOneApprove + AllMustApprove)
if (-not $SkipWorkflow2) {
    Write-Host "--- Test 2: Multi-User Approval Policy ---" -ForegroundColor Magenta
    try {
        & "$scriptDir\Test-E2EMultiUserApproval.ps1" -BaseUrl $BaseUrl
        Write-Host "  OK: Multi-User Approval test passed" -ForegroundColor Green
    } catch {
        Write-Host "  FAIL: $_" -ForegroundColor Red
        $failed++
    }
    Write-Host ""
} else {
    Write-Host "Skipping Test 2 (Multi-User Approval)" -ForegroundColor Gray
}

if ($failed -gt 0) {
    Write-Host "=== E2E All Workflows: $failed test(s) FAILED ===" -ForegroundColor Red
    exit 1
}

Write-Host "=== E2E All Workflow Types Test Passed ===" -ForegroundColor Cyan
Write-Host ""
