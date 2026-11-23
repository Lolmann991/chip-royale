# Chip Royale

Pokerspiel im Browser (Blazor WebAssembly + ASP.NET-Core-Server, .NET 10): Spiele gegen KI-Gegner, verdiene Chips und schalte Pixel-Art-Styles frei.

Das vollständige Konzept steht in [SPIELKONZEPT.md](SPIELKONZEPT.md).

## Projektstruktur

```
src/ChipRoyale.Client          Das Spiel: Blazor WebAssembly (läuft komplett im Browser, auch auf GitHub Pages)
src/ChipRoyale.Server          ASP.NET-Core-Server: Web-API, Konten (Identity), SQLite-Datenbank; liefert den Client aus
src/ChipRoyale.Shared          API-Verträge + Spielregeln fürs Konto (gemeinsam für Server und Offline-Modus)
src/ChipRoyale.Engine          Poker-Logik (Karten, Deck, Hand-Bewertung, KI, Wirtschaft)
tests/ChipRoyale.Engine.Tests  xUnit-Tests für die Engine
tests/ChipRoyale.Client.Tests  Tests für Texte (DE/EN), Tutorial und Konto-Regeln
```

## Spielen

- Lobby: Tisch (Übungstisch, Kneipe, Casino, High Roller, VIP-Salon), Buy-in, Schwierigkeit und 1–5 Gegner wählen.
- Blackjack: eigener Reiter in der Lobby – 6 Decks, Dealer steht auf Soft 17, Blackjack zahlt 3 : 2, Verdoppeln und Teilen; Tipps mit Basisstrategie und Kartenzählen; Tasten H/S/D/P.
- Sit & Go: Turnier zu sechst (Bronze 200, Silber 1.000, Gold 5.000 Buy-in), Blinds steigen alle 8 Hände, Platz 1 und 2 bekommen 65 % bzw. 35 % des Preisgelds.
- Konto: Angemeldete Spieler behalten ihren Chip-Stand (Start 1.000). Buy-in geht vom Konto an den Tisch, „Auszahlen“ bringt den Stapel zurück. Wer pleite ist, startet einen neuen Run mit 1.000 Chips; der Rekord bleibt.
- Rangliste unter `/rangliste` (nur mit Server). Gäste spielen mit einem Profil, das im Browser gespeichert wird.
- Shop unter `/shop`: Chips über dem Startkapital in Kronen tauschen (100 : 1, nur eine Richtung), damit Kartenrückseiten, Filz, Avatare und Chip-Designs kaufen (auch Pixel-Art). Erfolge bringen zusätzliche Kronen.
- Tutorial unter `/lernen`: vier Lektionen mit vorbereiteten Karten und Quizfragen, danach geht's zum Übungstisch.
- Sprache: Deutsch oder Englisch (Umschalter DE/EN, sonst Browser-Sprache). Texte stehen in `src/ChipRoyale.Client/Resources/SharedResource(.en).resx`.
- Tastatur: `F` passen, `C` checken/mitgehen, `R` setzen/erhöhen, `A` All-in vorwählen, `↑`/`↓` Einsatz ändern, `Leertaste` nächste Hand.
- 💡 Tipps zeigen die aktuelle Hand, die Gewinnchance und einen Hinweis (auf „Leicht“ standardmäßig an).
- Mausbewegung kippt den Tisch leicht; Klick auf die eigenen Karten verdeckt sie.

## Starten

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
(Windows: `winget install Microsoft.DotNet.SDK.10`). Dann im Hauptordner des Projekts:

```bash
dotnet run
```

und im Browser **http://localhost:5132** öffnen. (`dotnet run --project src/ChipRoyale.Server` geht weiterhin;
der kleine Starter `ChipRoyale.csproj` im Hauptordner leitet `dotnet run` dorthin weiter.)

Die SQLite-Datenbank (`src/ChipRoyale.Server/Datenbank/app.db`) wird beim Start automatisch angelegt bzw. migriert.
Eine ältere Datenbank (`%LOCALAPPDATA%\ChipRoyale\app.db`) wird beim ersten Start übernommen.

## Online, Gast und Offline

Das Spiel (Client) läuft komplett im Browser. Gespeichert wird je nach Lage:

| Modus | Wann | Speicherung |
|---|---|---|
| ● Online | Server erreichbar und angemeldet | Datenbank auf dem Server (Konto, Rangliste) |
| ● Gast | Server erreichbar, nicht angemeldet | im Browser (localStorage) |
| ● Offline | kein Server erreichbar | im Browser (localStorage) – das Spiel läuft weiter |

Die Lobby zeigt den Modus an. Das Spiel prüft den Server alle 8 Sekunden: Ein später gestarteter
Server wird automatisch genutzt, ein gestoppter schaltet in den Offline-Modus (nie mitten an einem
Tisch, erst zurück in der Lobby). Über ⚙️ lässt sich die Server-Adresse ändern. Beide Wege nutzen dieselben Regeln (`ChipRoyale.Shared/ProfileOperations.cs`).

## Entwickler-Konsole

`^` (Taste links neben der 1) oder `F2` öffnet auf jeder Seite eine Konsole für Tests, z. B.
`chips 250k`, `crowns +500`, `unlock all`, `unlock filz`, `equip felt-ocean`, `achieve all`, `stats`,
`reset confirm` – `help` zeigt alle Befehle. Angemeldet ändert sie das Konto in der Datenbank
(nur wenn `DevConsole:Enabled` in `src/ChipRoyale.Server/appsettings.json` an ist; mit
`DevConsole:Admins` lässt sie sich auf bestimmte E-Mail-Adressen beschränken), sonst das Profil im
Browser. Veränderte Profile tragen 🛠 in der Rangliste.

## GitHub Pages

Der Workflow `.github/workflows/pages.yml` veröffentlicht den Client bei jedem Push auf `deployment`
als statische Seite: **https://lolmann991.github.io/chip-royale/**

Einmalig einrichten: *Settings → Pages → Source: GitHub Actions*; unter *Settings → Environments →
github-pages* den Branch `deployment` erlauben. Die Seite nutzt den Server auf dem eigenen PC
(`http://localhost:5132`), sobald er läuft; andere Adresse über die Repository-Variable
`CHIPROYALE_API_URL` (`none` = nie nach einem Server suchen). Chrome/Edge fragen einmal, ob die
Seite auf Geräte im lokalen Netzwerk zugreifen darf – erlauben. Erlaubte
Seiten für den Server stehen in `src/ChipRoyale.Server/appsettings.json` unter `Cors:Origins`.

## Server + Datenbank als ZIP (Windows)

```bash
dotnet publish src/ChipRoyale.Server -c Release -r win-x64 --self-contained -o publish/ChipRoyale-Server
```

Im ZIP liegen `Server starten.cmd`, der Ordner `ChipRoyale-Server` (ohne .NET-Installation lauffähig)
und daneben `Datenbank\app.db` (`Storage:DataDirectory` = `../Datenbank`), damit ein Update des
Servers die Daten nicht anfasst.

## Tests

```bash
dotnet test ChipRoyale.slnx
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
