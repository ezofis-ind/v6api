param([string]$Path = "C:\Users\Ezofis\Downloads\c7ca0851761845bda19bc46404795f77.json")

$raw = [IO.File]::ReadAllText($Path)
Write-Host "File length:" $raw.Length
Write-Host "First 3 chars codes:" ([int][char]$raw[0]), ([int][char]$raw[1]), ([int][char]$raw[2])

try {
    [void][System.Text.Json.JsonDocument]::Parse($raw)
    Write-Host "System.Text.Json: VALID (use this file as raw body on POST /api/workflows)" -ForegroundColor Green
} catch {
    Write-Host "System.Text.Json: INVALID - $($_.Exception.Message)" -ForegroundColor Red
}

# Simulate wrong pattern: ConvertTo-Json on file string (double-encoding)
$wrapped = $raw | ConvertTo-Json -Compress
Write-Host "`nAfter ConvertTo-Json (wrong for Invoke-SaaSApi when Body is already JSON):"
Write-Host "Starts with:" $wrapped.Substring(0, [Math]::Min(40, $wrapped.Length))
try {
    [void][System.Text.Json.JsonDocument]::Parse($wrapped)
    Write-Host "Wrapped parses as:" ([System.Text.Json.JsonDocument]::Parse($wrapped).RootElement.ValueKind)
} catch {
    Write-Host "Wrapped parse error: $($_.Exception.Message)" -ForegroundColor Yellow
}
