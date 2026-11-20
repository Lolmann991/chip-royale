<#
.SYNOPSIS
    Holt den neuesten Stand, startet Chip Royale und öffnet es im Browser (http://localhost:5132).
    Beenden: dieses Fenster schließen oder Strg+C.
#>
param(
    [string]$Path = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$url = 'http://localhost:5132'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host '.NET 10 SDK fehlt. Installieren mit:  winget install Microsoft.DotNet.SDK.10' -ForegroundColor Yellow
    Read-Host 'Enter zum Schließen'
    exit 1
}

& (Join-Path $PSScriptRoot 'Update-ChipRoyale.ps1') -Path $Path

Write-Host 'Chip Royale startet – der Browser öffnet sich gleich ...' -ForegroundColor Green
# Browser öffnen, sobald der Server antwortet.
Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
    '-NoProfile', '-Command',
    "for (`$i = 0; `$i -lt 120; `$i++) { try { Invoke-WebRequest '$url' -UseBasicParsing -TimeoutSec 2 | Out-Null; Start-Process '$url'; break } catch { Start-Sleep -Seconds 2 } }"
)

Set-Location $Path
dotnet run --project src\ChipRoyale.Web --launch-profile http
