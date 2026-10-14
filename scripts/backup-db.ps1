<#
.SYNOPSIS
    Backs up the Clinic Management PostgreSQL database to a compressed custom-format dump.

.DESCRIPTION
    Runs pg_dump inside the running `db` container (binary-safe) and copies the result to the
    host. Uses the container's POSTGRES_USER/POSTGRES_DB, so it works for dev and prod alike.

.PARAMETER OutputDir
    Directory to write the dump into. Defaults to <repo>\backups.

.PARAMETER ComposeFile
    Compose file to target. Defaults to <repo>\docker-compose.yml.

.EXAMPLE
    pwsh -File scripts\backup-db.ps1
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputDir,
    [string]$ComposeFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ComposeFile) { $ComposeFile = Join-Path $RepoRoot 'docker-compose.yml' }
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'backups' }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$fileName = "clinic-$stamp.dump"
$containerPath = "/tmp/$fileName"
$hostPath = Join-Path $OutputDir $fileName

Write-Host "Dumping database to $containerPath inside the db container..."
$dumpCmd = 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f ' + $containerPath
& docker compose -f $ComposeFile exec -T db sh -c $dumpCmd
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed (exit $LASTEXITCODE)." }

Write-Host "Copying dump to $hostPath..."
& docker compose -f $ComposeFile cp "db:$containerPath" $hostPath
if ($LASTEXITCODE -ne 0) { throw "docker compose cp failed (exit $LASTEXITCODE)." }

& docker compose -f $ComposeFile exec -T db rm -f $containerPath | Out-Null

$sizeMb = [math]::Round((Get-Item -LiteralPath $hostPath).Length / 1MB, 2)
Write-Host "Backup complete: $hostPath ($sizeMb MB)."
