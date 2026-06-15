# Helper script to copy SQL scripts to output directory
# Run this after build if scripts are not automatically copied

$scriptSource = "scripts\CreateWorkflowSchemaComplete.sql"
$outputDir = "src\Api\bin\Debug\net8.0\scripts"

Write-Host "Copying workflow schema script..." -ForegroundColor Cyan

# Create output directory if it doesn't exist
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Created directory: $outputDir" -ForegroundColor Green
}

# Copy script
if (Test-Path $scriptSource) {
    Copy-Item $scriptSource -Destination $outputDir -Force
    Write-Host "✓ Script copied successfully to: $outputDir" -ForegroundColor Green
} else {
    Write-Host "✗ Source script not found: $scriptSource" -ForegroundColor Red
    exit 1
}

# Verify
if (Test-Path "$outputDir\CreateWorkflowSchemaComplete.sql") {
    Write-Host "✓ Verification passed - script exists in output directory" -ForegroundColor Green
} else {
    Write-Host "✗ Verification failed - script not found in output" -ForegroundColor Red
    exit 1
}

Write-Host "`nDone! You can now run the API." -ForegroundColor Cyan
