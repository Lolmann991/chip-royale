<#
.SYNOPSIS
    Holt die neueste Version von Chip Royale.

.DESCRIPTION
    Lädt alle Branches von GitHub und wechselt auf den neuesten Stand – je nachdem, was zuletzt
    geändert wurde: main oder ein Entwicklungs-Branch (claude/...). Änderungen an Dateien im
    Spielordner werden dabei überschrieben; deine Datenbank (Konto, Chips) bleibt erhalten,
    weil sie nicht im Repository liegt.

    Wird stündlich von der geplanten Aufgabe "Chip Royale aktualisieren" ausgeführt
    (siehe Install-ChipRoyale.ps1) und vor jedem Spielstart.
#>
param(
    [string]$Path = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ChipRoyale')
)

$ErrorActionPreference = 'Continue'
$logFile = Join-Path $Path 'update.log'

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Message
    Write-Host $line
    try { Add-Content -Path $logFile -Value $line -Encoding UTF8 } catch { }
}

if (-not (Test-Path (Join-Path $Path '.git'))) {
    Write-Host "Kein Chip-Royale-Ordner unter $Path gefunden. Bitte zuerst Install-ChipRoyale.ps1 ausführen."
    exit 1
}

Push-Location $Path
try {
    git fetch --prune --quiet origin
    if ($LASTEXITCODE -ne 0) {
        Write-Log 'GitHub nicht erreichbar – Update übersprungen.'
        exit 0
    }

    # Neuester Commit von origin/main und allen origin/claude/*-Branches.
    $newest = git for-each-ref --sort=-committerdate --count=1 --format='%(refname:short)' `
        refs/remotes/origin/main refs/remotes/origin/claude
    if (-not $newest) {
        Write-Log 'Kein passender Branch auf GitHub gefunden.'
        exit 0
    }

    $current = git rev-parse HEAD
    $target = git rev-parse $newest
    if ($current -eq $target) { exit 0 }

    git checkout --quiet --force -B spiel $newest
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Wechsel auf $newest fehlgeschlagen."
        exit 1
    }
    Write-Log ("Aktualisiert auf {0}: {1}" -f $newest, (git log -1 --format='%h %s'))
}
finally {
    Pop-Location
}
