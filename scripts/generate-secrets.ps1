<#
.SYNOPSIS
    Generates the production secrets the Clinic Management stack needs and writes them to .env.

.DESCRIPTION
    Creates a JWT signing key (48 random bytes) and a field-encryption master key (32 random
    bytes), base64-encoded, then stores them in .env as Jwt__SigningKey and
    Clinic__Encryption__MasterKeyBase64. Existing non-empty values are kept unless -Force is
    supplied, so re-running never silently rotates a key and locks out existing sessions/data.

.PARAMETER EnvFile
    Path to the .env file. Defaults to <repo>\.env (created from .env.example if missing).

.PARAMETER Force
    Overwrite values that already exist.

.EXAMPLE
    pwsh -File scripts\generate-secrets.ps1
#>
[CmdletBinding()]
param(
    [string]$EnvFile = (Join-Path (Split-Path -Parent $PSScriptRoot) '.env'),
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-Base64Secret {
    param([int]$ByteCount)
    $bytes = New-Object 'System.Byte[]' $ByteCount
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }
    return [Convert]::ToBase64String($bytes)
}

function Set-EnvValue {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Key,
        [string]$Value,
        [bool]$OnlyIfEmpty
    )
    $pattern = '^\s*' + [regex]::Escape($Key) + '='
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match $pattern) {
            $current = $Lines[$i].Substring($Lines[$i].IndexOf('=') + 1).Trim()
            if ($OnlyIfEmpty -and $current) {
                Write-Host "  $Key already set - keeping existing value."
                return
            }
            $Lines[$i] = "$Key=$Value"
            Write-Host "  $Key updated."
            return
        }
    }
    $Lines.Add("$Key=$Value")
    Write-Host "  $Key added."
}

if (-not (Test-Path -LiteralPath $EnvFile)) {
    $example = Join-Path (Split-Path -Parent $EnvFile) '.env.example'
    if (Test-Path -LiteralPath $example) {
        Copy-Item -LiteralPath $example -Destination $EnvFile
        Write-Host "Created $EnvFile from .env.example."
    }
    else {
        Write-Host "Created new $EnvFile."
    }
    New-Item -ItemType File -Force -Path $EnvFile | Out-Null
}

$lines = New-Object 'System.Collections.Generic.List[string]'
Get-Content -LiteralPath $EnvFile | ForEach-Object { $lines.Add($_) }

Write-Host 'Generating secrets:'
$onlyIfEmpty = -not $Force
Set-EnvValue -Lines $lines -Key 'Jwt__SigningKey' -Value (New-Base64Secret 48) -OnlyIfEmpty $onlyIfEmpty
Set-EnvValue -Lines $lines -Key 'Clinic__Encryption__MasterKeyBase64' -Value (New-Base64Secret 32) -OnlyIfEmpty $onlyIfEmpty

Set-Content -LiteralPath $EnvFile -Value $lines -Encoding UTF8
Write-Host ''
Write-Host "Done. Secrets written to $EnvFile."
Write-Host 'Keep this file private: never commit it, and back it up - losing the encryption key makes protected clinical fields unreadable.'
