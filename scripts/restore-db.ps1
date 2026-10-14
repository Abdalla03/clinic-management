<#
.SYNOPSIS
    Restores a Clinic Management PostgreSQL dump created by scripts\backup-db.ps1.

.DESCRIPTION
    Copies the dump into the `db` container and runs pg_restore with --clean --if-exists so the
    restore replaces objects in place. The database must already exist and be reachable.

.PARAMETER DumpFile
    Path to the .dump file (custom format) to restore.

.PARAMETER ComposeFile
    Compose file to target. Defaults to <repo>\docker-compose.yml.

.EXAMPLE
    pwsh -File scripts\restore-db.ps1 -DumpFile backups\clinic-20260101-120000.dump
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DumpFile,
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ComposeFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ComposeFile) { $ComposeFile = Join-Path $RepoRoot 'docker-compose.yml' }

$resolved = Resolve-Path -LiteralPath $DumpFile
$leaf = Split-Path -Leaf $resolved
$containerPath = "/tmp/$leaf"

Write-Host "Copying $resolved into the db container..."
& docker compose -f $ComposeFile cp $resolved "db:$containerPath"
if ($LASTEXITCODE -ne 0) { throw "docker compose cp failed (exit $LASTEXITCODE)." }

Write-Host 'Restoring (this replaces existing objects)...'
$restoreCmd = 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --clean --if-exists --no-owner ' + $containerPath
& docker compose -f $ComposeFile exec -T db sh -c $restoreCmd
$exit = $LASTEXITCODE

& docker compose -f $ComposeFile exec -T db rm -f $containerPath | Out-Null

if ($exit -ne 0) { throw "pg_restore reported errors (exit $exit)." }
Write-Host 'Restore complete.'
