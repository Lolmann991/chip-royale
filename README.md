# Chip Royale

Pokerspiel im Browser (Blazor Server, .NET 10): Spiele gegen KI-Gegner, verdiene Chips und schalte Pixel-Art-Styles frei.

Das vollständige Konzept steht in [SPIELKONZEPT.md](SPIELKONZEPT.md).

## Projektstruktur

```
src/ChipRoyale.Web            Blazor-Web-App (Interactive Server, Identity, SQLite)
src/ChipRoyale.Engine         Poker-Logik (Karten, Deck, Hand-Bewertung, ...)
tests/ChipRoyale.Engine.Tests xUnit-Tests für die Engine
```

## Spielen

- Lobby: Schwierigkeit (Leicht / Mittel / Schwer), 1–5 Gegner und Startkapital wählen.
- Tastatur: `F` passen, `C` checken/mitgehen, `R` setzen/erhöhen, `A` All-in vorwählen, `↑`/`↓` Einsatz ändern, `Leertaste` nächste Hand.
- 💡 Tipps zeigen die aktuelle Hand, die Gewinnchance und einen Hinweis (auf „Leicht“ standardmäßig an).
- Mausbewegung kippt den Tisch leicht; Klick auf die eigenen Karten verdeckt sie.

## Starten

```bash
dotnet run --project src/ChipRoyale.Web
```

## Tests

```bash
dotnet test
```
