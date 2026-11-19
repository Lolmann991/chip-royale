# Chip Royale

Pokerspiel im Browser (Blazor Server, .NET 10): Spiele gegen KI-Gegner, verdiene Chips und schalte Pixel-Art-Styles frei.

Das vollständige Konzept steht in [SPIELKONZEPT.md](SPIELKONZEPT.md).

## Projektstruktur

```
src/ChipRoyale.Web            Blazor-Web-App (Interactive Server, Identity, SQLite)
src/ChipRoyale.Engine         Poker-Logik (Karten, Deck, Hand-Bewertung, ...)
tests/ChipRoyale.Engine.Tests xUnit-Tests für die Engine
```

## Starten

```bash
dotnet run --project src/ChipRoyale.Web
```

## Tests

```bash
dotnet test
```
