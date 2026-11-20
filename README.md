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

```bash
dotnet run --project src/ChipRoyale.Web
```

Die SQLite-Datenbank (`src/ChipRoyale.Web/Data/app.db`) wird beim Start automatisch angelegt bzw. migriert.

## Tests

```bash
dotnet test
```

## Deployment (Azure App Service)

GitHub Pages kann nur statische Seiten ausliefern – Chip Royale braucht einen laufenden .NET-Server
(Live-Verbindung für den Spieltisch, SQLite-Datenbank). Deshalb baut GitHub Actions bei jedem Push auf
`main` die App, lässt alle Tests laufen und spielt sie auf **Azure App Service** aus
(`.github/workflows/deploy.yml`).

### Einmalig einrichten

1. Kostenloses Konto auf [portal.azure.com](https://portal.azure.com) anlegen.
2. **Ressource erstellen → Web-App**
   - Name: z. B. `chip-royale-deinname` (die genaue Adresse zeigt Azure nach dem Erstellen an)
   - Veröffentlichen: **Code** · Runtimestapel: **.NET 10 (LTS)** · Betriebssystem: **Linux**
   - Region: z. B. Germany West Central · Tarif: **Free F1**
3. In der Web-App unter **Einstellungen → Konfiguration → Allgemeine Einstellungen**:
   - **SCM Basic Auth Publishing Credentials**: Ein (sonst lässt sich kein Veröffentlichungsprofil herunterladen)
   - **Websockets** und **Sitzungsaffinität (ARR-Affinität)**: Ein, falls angezeigt
4. In der **Übersicht** der Web-App: **Veröffentlichungsprofil herunterladen**.
5. Auf GitHub im Repo: **Settings → Secrets and variables → Actions**
   - Reiter *Secrets*: `AZURE_WEBAPP_PUBLISH_PROFILE` = kompletter Inhalt der heruntergeladenen Datei
   - Reiter *Variables*: `AZURE_WEBAPP_NAME` = Name der Web-App
6. Fertig: Jeder Push bzw. Merge auf `main` deployt automatisch. Manuell geht es unter
   **Actions → Deploy to Azure → Run workflow**. Solange Secret oder Variable fehlen, wird das
   Deployment mit einem Hinweis übersprungen (der Build läuft trotzdem).

### Gut zu wissen

- Datenbank (`app.db`) und Login-Schlüssel liegen auf Azure unter `$HOME/data` und bleiben bei
  Neustarts und Deployments erhalten.
- Der Free-Tarif schläft nach ca. 20 Minuten ohne Besucher ein (der erste Aufruf dauert dann einige
  Sekunden), hat ein CPU-Tageslimit und nur wenige gleichzeitige Verbindungen. Für mehr Spieler: Tarif B1.
- Anderer Hoster oder Docker: Mit `Storage__DataDirectory=/pfad` als Umgebungsvariable legt die App
  Datenbank und Schlüssel in diesem Ordner ab.
