$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path ".env")) {
    Write-Host ".env not found. Copy .env.example to .env first." -ForegroundColor Red
    exit 1
}

# Load .env into the current process so the API composes its connection string and JWT options.
Get-Content ".env" | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq "" -or $line.StartsWith("#")) { return }
    $idx = $line.IndexOf("=")
    if ($idx -lt 1) { return }
    $key = $line.Substring(0, $idx).Trim()
    $value = $line.Substring($idx + 1).Trim()
    if ($value -ne "") { [Environment]::SetEnvironmentVariable($key, $value, "Process") }
}

if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = "Development" }
if (-not $env:ASPNETCORE_URLS) { $env:ASPNETCORE_URLS = "http://localhost:8080" }

Write-Host "Starting database (waiting for it to become healthy)..." -ForegroundColor Cyan
docker compose up -d --wait db
if ($LASTEXITCODE -ne 0) {
    Write-Host "Database did not become healthy. Is Docker running?" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "API:      $($env:ASPNETCORE_URLS)/swagger" -ForegroundColor Green
Write-Host "Frontend: cd web; npm install; npm run dev   (http://localhost:5173)" -ForegroundColor Green
Write-Host ""
Write-Host "Demo logins (Development only):" -ForegroundColor Yellow
Write-Host ("  Doctor    {0} / {1}" -f "doctor@clinic.local",    ($env:Seed__DoctorPassword    ?? "Doctor!Clinic2026"))
Write-Host ("  Secretary {0} / {1}" -f "secretary@clinic.local", ($env:Seed__SecretaryPassword ?? "Secretary!Clinic2026"))
Write-Host ("  Patient   {0} / {1}" -f "patient@clinic.local",   ($env:Seed__PatientPassword   ?? "Patient!Clinic2026"))
Write-Host ""
Write-Host "Migrations and demo seeding run automatically on startup in Development." -ForegroundColor DarkGray
Write-Host ""

dotnet run --project src\ClinicManagement.Api
