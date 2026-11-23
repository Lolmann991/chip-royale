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
  - Filz, Tischrand und Raum (Hintergrund)
  - Glücksbringer, Avatare & Chip-Designs

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

- **Rangliste:** höchster Chip-Stand (pro Run), aktueller Chip-Stand, abgeräumte Tische, gewonnene Turniere
- **Mehrspieler (nach V1):**
  - Gemeinsamer Tisch (echte Spieler + NPCs, live über SignalR)
  - Private Tische mit Einladungscode
  - Turniere mit echten Spielern
  - Seed-Challenge (gleiche Kartenfolge für alle)

## 6. Technik

| Thema | Entscheidung |
|---|---|
| Framework | .NET 10, Blazor WebAssembly (Client) + ASP.NET Core Web-API (Server) |
| Struktur | `ChipRoyale.Client` (UI im Browser), `ChipRoyale.Server` (API + Datenbank), `ChipRoyale.Shared` (Verträge, Konto-Regeln), `ChipRoyale.Engine` (Poker-Logik), Tests (xUnit) |
| Datenbank | SQLite + EF Core |
| Login | ASP.NET Core Identity API (Bearer-Token) |
| Hosting | Client auf GitHub Pages oder vom Server; Server + Datenbank lokal (ZIP) |
| Offline-Modus | ohne Server läuft das Spiel weiter, Profil im Browser (localStorage) |
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
- [x] 6er-Tisch (du + 5 KI-Gegner), alle starten mit 1.500 Turnier-Chips
- [x] Blind-Level steigen alle 8 Hände (10/20 → 15/30 → 25/50 → 50/100 → 75/150 → 100/200 → 150/300 → 200/400 → 300/600 → 500/1.000)
- [x] Buy-in-Stufen 200 / 1.000 / 5.000 vom Konto; Preisgeld = 6 × Buy-in, Platz 1 bekommt 65 %, Platz 2 35 %
- [x] Turnier-Anzeige (Level, Hände bis zur Erhöhung, verbleibende Spieler) und Platzierung am Ende
- [x] Rangliste: Turniersiege

**3. Umtausch, Style-Shop und Erfolge**
- [x] Chips → Kronen (100 : 1), nur eine Richtung, nur Chips über dem Startkapital (sonst ließe sich über Pleite-Resets farmen)
- [x] Shop (`/shop`): 23 Artikel – Kartenrückseiten, Filz, Avatare, Chip-Designs; Pixel-Art-Varianten als Extra
- [x] 13 Erfolge mit Kronen-Belohnung (Übungstisch zählt nicht)

**4. Omaha (Pot-Limit)**
- [ ] 4 Karten pro Spieler, genau 2 eigene + 3 aus der Mitte
- [ ] Pot-Limit-Setzregel, KI und Gewinnchance für Omaha

**5. Five Card Draw**
- [ ] Ablauf ohne Gemeinschaftskarten: Setzrunde, Tauschrunde (0–3 Karten abwerfen), Setzrunde, Showdown
- [ ] Karten zum Tauschen anklicken; KI-Tauschstrategie

**Außerdem in V2**
- [x] Live-Statistik bei aktiven Tipps: Rang der Hand (1–10), Starthand-Rang (1–169, Top-%), Sieg/Split/Niederlage, Pot-Odds, Outs, Chancen bis zum River
- [x] Karten liegen auf dem Filz: Austeilen vom Deck, Passen in den Ablagestapel, Aufdecken am Tisch, Einsammeln vor jeder Hand
- [x] Vollere Chipstapel, Spieler-Stacks und Chip-Ablage auf dem Tisch; Gewinn-Anzeige länger und per Maus pausierbar
- [ ] KI „Schwer“ nachschärfen (in Simulationen gewinnt ein Spieler der Stufe „Mittel“ noch gegen „Schwer“)
- [ ] Statistik-Seite (Hände, Siege, größter Pot, Turniere) und Replay der letzten Hand
- [ ] Tutorial-Fortschritt im Konto speichern; restliche Kontoseiten übersetzen

**Erledigt außer der Reihe**
- [x] Konto-Seiten (Anmelden, Registrieren, Kontoverwaltung) im Spiel-Design, Fehlermeldungen DE/EN, einfache Passwortregeln (mind. 6 Zeichen)
- [x] Datenbank im Benutzerordner (Windows: `%LOCALAPPDATA%\ChipRoyale`), damit ein Update Konten nicht löscht; alte `Data\app.db` wird beim ersten Start übernommen

- [x] Mehr Deko im Shop (80 Artikel): 22 Kartenrückseiten (u. a. Art déco, Bauhaus, Japanische Wellen, Mandala, Synthwave, Marmor, Line Art, Pixel), 8 Kartendecks (Vier-Farben-Poker-Deck, Jumbo, Vintage, Noir, Neon, Gold, Pixel), 8 Filze, 6 Tischränder, 6 Räume, 11 Glücksbringer neben dem eigenen Platz, 15 Avatare, 6 Chip-Designs; Erfolge „Sammler“ und „Trendsetter“

**Ideen für V2.x** (nach Omaha / Five Card Draw)
- [ ] Gegner mit Persönlichkeit: Spielstil-Etikett (z. B. „Calling Station“, „Bluffer“) und kurze Sprüche in der Sprechblase
- [ ] Vor-Aktionen („Check/Fold“, „Jeden Einsatz mitgehen“) und Tempo-Einstellung (normal / schnell)
- [ ] Tägliche Herausforderung mit Kronen-Belohnung (z. B. „Gewinne 3 Pots mit Zwei Paaren oder besser“)
- [ ] Verlauf der letzten Hände mit Karten und Replay
- [ ] Statistik-Seite: Gewinnrate, gespielte Starthände, Lieblingshand, Turnier-Bilanz

### Deployment (Branch `deployment`)
- [x] Umbau auf Blazor WebAssembly: das Spiel läuft komplett im Browser
- [x] Server als Web-API (Konten, Chips, Shop, Erfolge, Rangliste) mit SQLite-Datenbank
- [x] GitHub Pages: Client wird per GitHub Actions veröffentlicht
- [x] Offline-Modus: ohne Server weiterspielen, Profil im Browser; Statusanzeige Online/Gast/Offline
- [x] Windows-ZIP mit Server + Datenbank (Datenbank neben dem Server, übersteht Updates)
- [ ] Server ins Internet stellen (dann auch für Freunde ohne eigenen Server) – Ergebnisse vom Server prüfen lassen

### Blackjack (umgesetzt)
- [x] Eigener Modus in der Lobby (Reiter „Blackjack“), online und offline – gleiche Chips wie beim Poker
- [x] Tische: Übungstisch (kostenlos), Kneipe (Einsatz 5–100), Casino (25–500), VIP-Salon (100–2.000); Buy-in wie beim Poker
- [x] Regeln: 6 Decks, Dealer steht auf Soft 17, Blackjack zahlt 3 : 2, Dealer schaut bei Ass/Zehn nach Blackjack,
      Verdoppeln auf zwei Karten (auch nach dem Teilen), bis zu 4 Hände, geteilte Asse bekommen eine Karte
- [x] Tipps: Basisstrategie, Überkauf-Risiko der nächsten Karte, Dealer-Bust-Chance, Hi-Lo-Kartenzählung
- [x] Tasten: H Karte, S Stehen, D Verdoppeln, P Teilen, Leertaste Geben/Nächste Runde, ↑/↓ Einsatz
- [x] Statistik (Runden, gewonnen, Blackjacks, größter Gewinn) in der Datenbank, Rangliste „Blackjack“, 4 neue Erfolge
- [ ] Ideen: Versicherung, Aufgeben (Surrender), mehrere Boxen gleichzeitig, Nebenwetten (Perfect Pairs, 21+3)

### Entwickler-Konsole (umgesetzt)
- [x] Öffnen mit `^` (Taste links neben der 1) oder `F2`, auf jeder Seite; Tab ergänzt, ↑/↓ Verlauf, Esc schließt
- [x] Befehle: `chips`, `crowns`, `unlock`, `lock`, `equip`, `items`, `achieve`, `unachieve`, `achievements`, `stats`, `reset confirm`, dazu `help`, `clear`, `status`, `server`
- [x] Online gegen die Datenbank (Server-Schalter `DevConsole:Enabled`, optional nur für `DevConsole:Admins`), offline gegen das Browser-Profil
- [x] Veränderte Profile tragen 🛠 in der Rangliste

### Mehrspieler mit Freunden (Plan)

**Ziel:** Einen privaten Tisch erstellen, Link an Freunde schicken, zusammen spielen – über den eigenen Server.

**Ablauf für Spieler**
1. Lobby → „Mit Freunden spielen“ → Tisch einstellen (Blinds, Startstack, Plätze 2–6, freie Plätze mit Bots füllen ja/nein).
2. Der Server erzeugt einen Tisch-Code (z. B. `K7P2QX`) und einen Einladungslink:
   `https://lolmann991.github.io/chip-royale/tisch/K7P2QX?server=<Server-Adresse>`
   (der Link enthält die Server-Adresse – Freunde brauchen nichts einzustellen).
3. Freunde öffnen den Link, melden sich an (oder spielen als Gast mit Namen), setzen sich.
4. Der Gastgeber startet; Wartezimmer zeigt, wer da ist. Rematch mit einem Klick.

**Technik**
- **Server entscheidet alles** (server-authoritative): Der Tisch (`HoldemTable` aus der Engine) läuft auf dem Server, nicht im Browser. Jeder bekommt nur seine eigenen Karten zu sehen – sonst könnte man mogeln.
- **Echtzeit mit SignalR** (WebSockets): `TableHub` mit `Create`, `Join`, `Sit`, `Act`, `Leave`, `Chat`; der Server schickt Ereignisse (Karten ausgeteilt, Aktion, Pot, Showdown) an alle am Tisch.
- **Bots** laufen ebenfalls auf dem Server (vorhandene `BotBrain`).
- **Zeitlimit** pro Zug (z. B. 30 s, danach Check/Passen), **Verbindungsabbruch**: Platz bleibt 60 s reserviert, Hände werden automatisch gepasst, Wiederverbinden setzt fort.
- **Client**: neuer „Remote“-Modus der Tischansicht – bestehende Bausteine (Sitzplätze, Karten, Chips, Animationen, Einsatz-Steuerung) bleiben, nur der Spielablauf kommt vom Server statt aus der lokalen Schleife.
- **Erreichbarkeit**: Freunde müssen den Server per **HTTPS** erreichen (GitHub Pages ist HTTPS; `localhost` gilt nur für den eigenen PC). Optionen:
  - **Cloudflare Tunnel** (kostenlos, ohne Router-Einstellungen; Schnell-Tunnel mit wechselnder Adresse, feste Adresse mit eigener Domain),
  - **Tailscale Funnel** (kostenlos, feste Adresse `name.tailnet.ts.net`),
  - Router-Portfreigabe + DynDNS + Zertifikat (aufwendig),
  - später: Server in der Cloud.
  Das Startskript kann den Tunnel mitstarten und die Einladungsadresse anzeigen.
- **Sicherheit**: Dev-Befehle an Mehrspieler-Tischen gesperrt; Rate-Limits; Tisch-Codes sind nicht erratbar (6 Zeichen ohne verwechselbare Buchstaben, ~1 Mrd. Kombinationen) und laufen ab.

**Phasen**
- [ ] **M1 – Freundschaftsspiel:** Privater Tisch per Code/Link, alle mit gleichem Startstack, Chips zählen nicht fürs Konto. 2–6 Menschen + optional Bots, Zeitlimit, Wiederverbinden, Wartezimmer, Rematch.
- [ ] **M2 – Mit Konto-Chips:** Buy-in vom Konto, Auszahlen, Hände zählen für Statistik, Erfolge und Rangliste.
- [ ] **M3 – Extras:** Sit & Go mit Freunden, Chat und Emotes, Zuschauer, Hand-Verlauf der Runde.
- [ ] **M4 – Öffentlich:** offene Tische/Matchmaking (nur sinnvoll mit dauerhaft erreichbarem Server).

**Offene Entscheidungen**
- Erst Freundschaftsspiel (M1) oder direkt mit Konto-Chips?
- Welcher Weg ins Internet: Cloudflare Tunnel oder Tailscale Funnel?
- Dürfen Freunde ohne Konto (nur mit Namen) mitspielen?

### V3
- Mehrspieler: gemeinsame Tische, private Tische, Turniere, Seed-Challenge
- Balancing, Glücksfaktor der KI
- Echter E-Mail-Versand (Bestätigung, Passwort zurücksetzen)
