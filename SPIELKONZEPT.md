# Chip Royale – Spielkonzept

Browser-Pokerspiel (Blazor Server) gegen KI-Gegner mit Chip-Progression, Pixel-Art-Styles und Rangliste.

## 1. Kern-Gameplay

| Thema | Entscheidung |
|---|---|
| Spielprinzip | Klassisches Poker gegen NPCs, keine Joker-/Roguelike-Mechanik |
| Varianten | Texas Hold'em, Five Card Draw, Omaha (Auswahl im Spielmodus-Menü) |
| Formate | Cash Game **und** Turnier (Sit & Go) |
| Tischgröße | bis zu 6 Spieler (inkl. Spieler) |
| Setzregel in der Hand | No-Limit |
| Tischstufen | Mehrere Tische mit Mindest-Buy-in; Maximum = 20 × Mindest-Buy-in (verlangsamt die Progression) |

## 2. Wirtschaft & Fortschritt

- **Startkapital:** 1.000 Chips (konfigurierbar)
- **Pleite (0 Chips):** Kompletter Reset auf Startkapital. Der laufende Ranglisten-Run endet. Gekaufte Styles bleiben.
- **Umtausch:** Chips → Geld zu festem Kurs, **nur in eine Richtung** (Geld kann nicht zurück in Chips).
- **Style-Shop** (mit Geld, eigene Pixel-Art):
  - Kartenrückseiten
  - Kartenvorderseiten / Deck-Designs
  - Tisch / Spielfeld
  - Avatare & Chip-Designs

## 3. KI & Schwierigkeit

- **Intelligenz:** regelbasiert in Stufen
  - *Einfach:* spendabel – zahlt gute Hände aus, passt selten, blufft kaum (Pots werden groß)
  - *Mittel:* berücksichtigt Pot-Odds und Position
  - *Schwer:* blufft, liest das Spielerverhalten
- **Glück der KI:** in V1 immer faires Mischen. Ein Glücksfaktor kommt erst mit dem Balancing in einer späteren Version.

## 4. Tutorial

- Geführte Lektionen (Handrangfolge, Setzrunden, Blinds, Positionen) mit vorbereiteten Karten
- Hand-Rangliste jederzeit zum Nachschlagen
- Optionale Live-Tipps im Spiel (aktuelle Hand, Gewinnchance)
- Übungstisch ohne Chip-Verlust gegen sehr einfache KI

## 5. Rangliste & Mehrspieler

- **Rangliste:** höchster Chip-Stand (pro Run), aktueller Chip-Stand, gewonnene Turniere
- **Mehrspieler (nach V1):**
  - Gemeinsamer Tisch (echte Spieler + NPCs, live über SignalR)
  - Private Tische mit Einladungscode
  - Turniere mit echten Spielern
  - Seed-Challenge (gleiche Kartenfolge für alle)

## 6. Technik

| Thema | Entscheidung |
|---|---|
| Framework | .NET 10, Blazor Web App (Interactive Server) |
| Struktur | `ChipRoyale.Web` (UI), `ChipRoyale.Engine` (Poker-Logik, UI-unabhängig), `ChipRoyale.Engine.Tests` (xUnit) |
| Datenbank | SQLite + EF Core |
| Login | ASP.NET Core Identity (Benutzerkonten) |
| Sprache | Deutsch + Englisch (Ressourcendateien) |
| Plattform | nur Desktop |
| Grafik | eigener Pixel-Art-Stil (keine fremden Assets) |

## 7. Roadmap

### V1 – Spielbares Poker + Tutorial
- [x] Projektgerüst
- [x] Karten, Deck, Hand-Bewertung (Engine)
- [x] Texas-Hold'em-Spielablauf (Blinds, Setzrunden, Side-Pots, Showdown)
- [x] KI-Stufen Einfach / Mittel / Schwer
- [x] Spieltisch-UI für 6 Plätze (3D-Tisch mit Animationen und Gewinn-Effekten)
- [ ] Benutzerkonto mit Chip-Stand, Reset bei 0 Chips
- [ ] Tischstufen mit Buy-in-Grenzen (Min / 20 × Min)
- [ ] Tutorial: Lektionen, Hand-Rangliste, Live-Tipps, Übungstisch
- [ ] Rangliste
- [ ] Lokalisierung DE/EN

### V2
- Five Card Draw, Omaha
- Turniere (Sit & Go)
- Chip→Geld-Umtausch und Style-Shop

### V3
- Mehrspieler: gemeinsame Tische, private Tische, Turniere, Seed-Challenge
- Balancing, Glücksfaktor der KI
