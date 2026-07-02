# Apply Users module migrations to a tenant database.
# Wrapper: delegates to repo-root scripts\UpdateTenantDatabase.ps1

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
& (Join-Path $repoRoot "scripts\UpdateTenantDatabase.ps1") @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
