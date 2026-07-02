# Apply workflow schema to a tenant database
# Same as ApplyWorkflowSchemaToTenant.ps1 - use either script
#
# Usage: .\RunTenantSchema.ps1 -TenantId "guid"
#        .\RunTenantSchema.ps1 -TenantDatabase "ezofis_Tenant_1"

param(
    [string]$TenantId = "",
    [string]$TenantDatabase = ""
)

& "$PSScriptRoot\ApplyWorkflowSchemaToTenant.ps1" -TenantId $TenantId -TenantDatabase $TenantDatabase
