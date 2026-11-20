<#
.SYNOPSIS
    Entfernt die automatische Aktualisierung und die Desktop-Verknüpfung.
    Der Spielordner (Dokumente\ChipRoyale) bleibt stehen – den kannst du selbst löschen.
#>
Unregister-ScheduledTask -TaskName 'Chip Royale aktualisieren' -Confirm:$false -ErrorAction SilentlyContinue
$link = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Chip Royale.lnk'
if (Test-Path $link) { Remove-Item $link }
Write-Host 'Automatische Aktualisierung und Verknüpfung entfernt.'
