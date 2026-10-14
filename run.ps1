$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Starting database..." -ForegroundColor Cyan
docker compose up -d db

if (-not (Test-Path ".env")) { Write-Host ".env not found" -ForegroundColor Red; exit 1 }
Get-Content ".env" | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq "" -or $line.StartsWith("#")) { return }
    $idx = $line.IndexOf("=")
    if ($idx -lt 1) { return }
    $key = $line.Substring(0, $idx).Trim()
    $value = $line.Substring($idx + 1).Trim()
    if ($value -ne "") { [Environment]::SetEnvironmentVariable($key, $value, "Process") }
}

Write-Host "Starting API on http://localhost:8080/swagger" -ForegroundColor Green
dotnet run --project src\ClinicManagement.Api
