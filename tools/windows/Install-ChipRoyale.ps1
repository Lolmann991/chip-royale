<#
.SYNOPSIS
    Richtet Chip Royale unter "Dokumente\ChipRoyale" ein und hält es automatisch aktuell.

.DESCRIPTION
    - lädt das Spiel von GitHub (falls noch nicht da),
    - holt sofort den neuesten Stand,
    - legt die geplante Aufgabe "Chip Royale aktualisieren" an (stündlich; verpasste Läufe werden
      nach dem Hochfahren nachgeholt),
    - legt eine Desktop-Verknüpfung "Chip Royale" zum Starten an.

    Erneut ausführen ist unproblematisch. Entfernen: Uninstall-ChipRoyale.ps1.
#>
param(
    [string]$Path = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ChipRoyale'),
    [string]$RepoUrl = 'https://github.com/Lolmann991/chip-royale.git',
    [int]$EveryHours = 1
)

$ErrorActionPreference = 'Stop'
$taskName = 'Chip Royale aktualisieren'

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host 'Git ist nicht installiert. Installieren mit:  winget install --id Git.Git -e' -ForegroundColor Yellow
    Write-Host 'Danach PowerShell neu öffnen und dieses Skript nochmal starten.'
    exit 1
}

if (-not (Test-Path (Join-Path $Path '.git'))) {
    Write-Host "Lade Chip Royale nach $Path ..."
    git clone --quiet $RepoUrl $Path
    if ($LASTEXITCODE -ne 0) { throw 'Herunterladen von GitHub fehlgeschlagen.' }
}

$update = Join-Path $Path 'tools\windows\Update-ChipRoyale.ps1'
$start = Join-Path $Path 'tools\windows\Start-ChipRoyale.ps1'
& $update -Path $Path

# Geplante Aufgabe (läuft unter deinem Benutzer, keine Admin-Rechte nötig).
$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$update`" -Path `"$Path`""
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Hours $EveryHours)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 15)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings `
    -Description 'Holt die neueste Version von Chip Royale nach Dokumente\ChipRoyale.' -Force | Out-Null
Write-Host "Geplante Aufgabe '$taskName' eingerichtet (alle $EveryHours Stunde(n))." -ForegroundColor Green

# Desktop-Verknüpfung zum Starten.
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut((Join-Path $desktop 'Chip Royale.lnk'))
$link.TargetPath = 'powershell.exe'
$link.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$start`""
$link.WorkingDirectory = $Path
$link.IconLocation = "$env:SystemRoot\System32\shell32.dll,137"
$link.Description = 'Chip Royale starten'
$link.Save()
Write-Host 'Desktop-Verknüpfung "Chip Royale" angelegt.' -ForegroundColor Green

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ''
    Write-Host 'Zum Spielen fehlt noch das .NET 10 SDK. Installieren mit:' -ForegroundColor Yellow
    Write-Host '    winget install Microsoft.DotNet.SDK.10'
}
Write-Host ''
Write-Host "Fertig. Das Spiel liegt unter $Path und wird automatisch aktuell gehalten."
