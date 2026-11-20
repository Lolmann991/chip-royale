# Chip Royale – Spielkonzept

Browser-Pokerspiel (Blazor Server) gegen KI-Gegner mit Chip-Progression, freischaltbaren Styles und Rangliste.

## 1. Kern-Gameplay

| Thema | Entscheidung |
|---|---|
| Spielprinzip | Klassisches Poker gegen NPCs, keine Joker-/Roguelike-Mechanik |
| Varianten | Texas Hold'em, Five Card Draw, Omaha (Auswahl im Spielmodus-Menü) |
| Formate | Cash Game **und** Turnier (Sit & Go) |
| Tischgröße | bis zu 6 Spieler (inkl. Spieler) |
| Setzregel in der Hand | No-Limit (Hold'em), Pot-Limit (Omaha, ab V2) |
| Tischstufen | Mehrere Tische mit Mindest-Buy-in; Maximum = 20 × Mindest-Buy-in (verlangsamt die Progression) |

## 2. Wirtschaft & Fortschritt

- **Startkapital:** 1.000 Chips (konfigurierbar)
- **Pleite (0 Chips):** Kompletter Reset auf Startkapital. Der laufende Ranglisten-Run endet. Gekaufte Styles bleiben.
- **Umtausch:** Chips → Kronen (👑, die Shop-Währung) zu festem Kurs **100 Chips = 1 Krone**, **nur in eine Richtung**. Getauscht werden nur Chips auf dem Konto (nicht am Tisch). Der Umtausch senkt den aktuellen Chip-Stand, der Rekord in der Rangliste bleibt.
- **Kronen** gibt es außerdem für Erfolge (z. B. erster Royal Flush, Tisch abgeräumt, Turnier gewonnen, alle Lektionen geschafft).
- **Style-Shop** (mit Kronen; der 3D-Look ist der Grundstil, Pixel-Art-Designs sind freischaltbare Extras):
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
- [x] Benutzerkonto mit Chip-Stand, Reset bei 0 Chips (Gäste spielen ohne Speicherung)
- [x] Tischstufen mit Buy-in-Grenzen (Min / 20 × Min): Kneipe, Casino, High Roller, VIP-Salon
- [x] Tutorial: 4 Lektionen (Hand-Rangfolge, Setzrunden, Blinds, Positionen) mit Quiz, Hand-Rangliste, Live-Tipps, Übungstisch
- [x] Rangliste (höchster Stand pro Run, aktueller Stand, abgeräumte Tische)
- [x] Lokalisierung DE/EN (Ressourcendateien, Umschalter, Browser-Sprache; Konto-Verwaltungsseiten noch englisch)

### V2 – Turniere, Shop und neue Varianten

Reihenfolge = Umsetzungsreihenfolge.

**1. Fundament**
- [x] CI auf GitHub (Build + alle Tests bei jedem Push/PR)
- [x] E-Mail-Bestätigung abschaltbar (`Auth:RequireConfirmedAccount`, Standard aus, bis ein echter E-Mail-Versand eingerichtet ist)
- [x] Spieltisch-Komponente aufteilen (Logik, Sitzplatz, Aktionsleiste)
- [x] Engine: Blinds zwischen Händen änderbar (für Turniere)

**2. Sit & Go**
- [ ] 6er-Tisch (du + 5 KI-Gegner), alle starten mit 1.500 Turnier-Chips
- [ ] Blind-Level steigen alle 8 Hände (10/20 → 15/30 → 25/50 → 50/100 → 75/150 → 100/200 → 150/300 → 200/400 → 300/600 → 500/1.000)
- [ ] Buy-in-Stufen 200 / 1.000 / 5.000 vom Konto; Preisgeld = 6 × Buy-in, Platz 1 bekommt 65 %, Platz 2 35 %
- [ ] Turnier-Anzeige (Level, Hände bis zur Erhöhung, verbleibende Spieler) und Platzierung am Ende
- [ ] Rangliste: Turniersiege

**3. Umtausch, Style-Shop und Erfolge**
- [ ] Chips → Kronen (100 : 1), nur eine Richtung
- [ ] Shop: Kartenrückseiten, Filz/Tisch, Avatare, Chip-Designs; Pixel-Art-Set als Extra
- [ ] Erfolge mit Kronen-Belohnung

**4. Omaha (Pot-Limit)**
- [ ] 4 Karten pro Spieler, genau 2 eigene + 3 aus der Mitte
- [ ] Pot-Limit-Setzregel, KI und Gewinnchance für Omaha

**5. Five Card Draw**
- [ ] Ablauf ohne Gemeinschaftskarten: Setzrunde, Tauschrunde (0–3 Karten abwerfen), Setzrunde, Showdown
- [ ] Karten zum Tauschen anklicken; KI-Tauschstrategie

**Außerdem in V2**
- [ ] KI „Schwer“ nachschärfen (in Simulationen gewinnt ein Spieler der Stufe „Mittel“ noch gegen „Schwer“)
- [ ] Statistik-Seite (Hände, Siege, größter Pot, Turniere) und Replay der letzten Hand
- [ ] Tutorial-Fortschritt im Konto speichern; restliche Kontoseiten übersetzen

### V3
- Mehrspieler: gemeinsame Tische, private Tische, Turniere, Seed-Challenge
- Balancing, Glücksfaktor der KI
- Echter E-Mail-Versand (Bestätigung, Passwort zurücksetzen)
