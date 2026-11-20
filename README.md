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
- Konto: Angemeldete Spieler behalten ihren Chip-Stand (Start 1.000). Buy-in geht vom Konto an den Tisch, „Auszahlen“ bringt den Stapel zurück. Wer pleite ist, startet einen neuen Run mit 1.000 Chips; der Rekord bleibt.
- Rangliste unter `/rangliste`. Gäste können spielen, werden aber nicht gespeichert.
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
