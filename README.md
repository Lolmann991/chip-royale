# Chip Royale

Pokerspiel im Browser (Blazor Server, .NET 10): Spiele gegen KI-Gegner, verdiene Chips und schalte Pixel-Art-Styles frei.

Das vollständige Konzept steht in [SPIELKONZEPT.md](SPIELKONZEPT.md).

## Projektstruktur

```
src/ChipRoyale.Web            Blazor-Web-App (Interactive Server, Identity, SQLite)
src/ChipRoyale.Engine         Poker-Logik (Karten, Deck, Hand-Bewertung, ...)
tests/ChipRoyale.Engine.Tests xUnit-Tests für die Engine
tests/ChipRoyale.Web.Tests    Tests für Texte (DE/EN vollständig) und Tutorial-Inhalte
```

## Spielen

- Lobby: Tisch (Übungstisch, Kneipe, Casino, High Roller, VIP-Salon), Buy-in, Schwierigkeit und 1–5 Gegner wählen.
- Sit & Go: Turnier zu sechst (Bronze 200, Silber 1.000, Gold 5.000 Buy-in), Blinds steigen alle 8 Hände, Platz 1 und 2 bekommen 65 % bzw. 35 % des Preisgelds.
- Konto: Angemeldete Spieler behalten ihren Chip-Stand (Start 1.000). Buy-in geht vom Konto an den Tisch, „Auszahlen“ bringt den Stapel zurück. Wer pleite ist, startet einen neuen Run mit 1.000 Chips; der Rekord bleibt.
- Rangliste unter `/rangliste`. Gäste können spielen, werden aber nicht gespeichert.
- Shop unter `/shop`: Chips über dem Startkapital in Kronen tauschen (100 : 1, nur eine Richtung), damit Kartenrückseiten, Filz, Avatare und Chip-Designs kaufen (auch Pixel-Art). Erfolge bringen zusätzliche Kronen.
- Tutorial unter `/lernen`: vier Lektionen mit vorbereiteten Karten und Quizfragen, danach geht's zum Übungstisch.
- Sprache: Deutsch oder Englisch (Umschalter DE/EN, sonst Browser-Sprache). Texte stehen in `src/ChipRoyale.Web/Resources/SharedResource(.en).resx`.
- Tastatur: `F` passen, `C` checken/mitgehen, `R` setzen/erhöhen, `A` All-in vorwählen, `↑`/`↓` Einsatz ändern, `Leertaste` nächste Hand.
- 💡 Tipps zeigen die aktuelle Hand, die Gewinnchance und einen Hinweis (auf „Leicht“ standardmäßig an).
- Mausbewegung kippt den Tisch leicht; Klick auf die eigenen Karten verdeckt sie.

## Starten

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
(Windows: `winget install Microsoft.DotNet.SDK.10`). Dann im Hauptordner des Projekts:

```bash
dotnet run
```

und im Browser **http://localhost:5132** öffnen. (`dotnet run --project src/ChipRoyale.Web` geht weiterhin;
der kleine Starter `ChipRoyale.csproj` im Hauptordner leitet `dotnet run` dorthin weiter.)

Die SQLite-Datenbank (`src/ChipRoyale.Web/Data/app.db`) wird beim Start automatisch angelegt bzw. migriert.

## Tests

```bash
dotnet test
```

## Auf dem eigenen PC (Windows): immer die neueste Version

Einmal in PowerShell ausführen (braucht [Git](https://git-scm.com), z. B. `winget install --id Git.Git -e`):

```powershell
$ziel = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'ChipRoyale'
git clone -b claude/gallant-mayer-y2wk82 https://github.com/Lolmann991/chip-royale.git $ziel
powershell -ExecutionPolicy Bypass -File "$ziel\tools\windows\Install-ChipRoyale.ps1"
```

Danach liegt das Spiel unter `Dokumente\ChipRoyale` und
- die geplante Aufgabe **„Chip Royale aktualisieren“** holt jede Stunde den neuesten Stand
  (verpasste Läufe werden nach dem Hochfahren nachgeholt) – immer den zuletzt geänderten Branch,
  also `main` oder den aktuellen Entwicklungs-Branch,
- die Desktop-Verknüpfung **„Chip Royale“** aktualisiert, startet das Spiel und öffnet es im Browser
  (`http://localhost:5132`). Dafür wird das .NET 10 SDK gebraucht: `winget install Microsoft.DotNet.SDK.10`.

Dein Konto und deine Chips (lokale Datenbank) bleiben bei Updates erhalten. Protokoll: `update.log`
im Spielordner. Entfernen: `tools\windows\Uninstall-ChipRoyale.ps1`.
