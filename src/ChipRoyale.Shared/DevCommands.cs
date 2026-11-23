using System.Globalization;
using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Shared;

/// <param name="Command">The line typed into the developer console, e.g. "chips 50000".</param>
/// <param name="Lang">"de" or "en" for the answer.</param>
public sealed record DevRequest(string Command, string Lang);

/// <param name="Profile">The profile after the command (also when it failed).</param>
/// <param name="Output">Lines to print in the console.</param>
public sealed record DevResponse(ProfileResponse Profile, string[] Output, bool Ok);

/// <summary>Result of one developer command.</summary>
public sealed record DevResult(OperationResult Operation, string[] Output, bool Ok);

/// <summary>
/// Developer console commands that change a profile (unlock items, set chips, …). Used by the
/// server (endpoint api/dev, if enabled there) and by the offline profile in the browser.
/// Every command that changes something marks the profile (🛠 on the leaderboard).
/// </summary>
public static class DevCommands
{
    public const int MaxValue = 1_000_000_000;

    /// <summary>Commands with arguments and help texts (German, English).</summary>
    public static readonly IReadOnlyList<(string Name, string Args, string De, string En)> Help =
    [
        ("chips", "<n | +n | -n>", "Chips setzen oder ändern (ohne die am Tisch)", "Set or change chips (not those on a table)"),
        ("crowns", "<n | +n | -n>", "Kronen setzen oder ändern", "Set or change crowns"),
        ("unlock", "<all | Kategorie | Item-Id …>", "Shop-Items freischalten (kostenlos)", "Unlock shop items (free)"),
        ("lock", "<all | Kategorie | Item-Id …>", "Shop-Items wieder wegnehmen", "Take shop items away again"),
        ("equip", "<Item-Id>", "Item anlegen (wird bei Bedarf freigeschaltet)", "Wear an item (unlocked if needed)"),
        ("items", "[Kategorie]", "Items mit Id, Preis und Besitz anzeigen", "List items with id, price and ownership"),
        ("achieve", "<all | Erfolg-Id …>", "Erfolge freischalten (ohne Kronen-Belohnung)", "Unlock achievements (no crown reward)"),
        ("unachieve", "<all | Erfolg-Id …>", "Erfolge wieder sperren", "Lock achievements again"),
        ("achievements", "", "Alle Erfolge mit Status anzeigen", "List all achievements with status"),
        ("stats", "", "Alle Werte des Profils anzeigen", "Show all profile values"),
        ("reset", "confirm", "Profil komplett zurücksetzen (Chips, Kronen, Items, Erfolge)", "Reset the whole profile (chips, crowns, items, achievements)"),
    ];

    public static IEnumerable<string> Names => Help.Select(h => h.Name);

    /// <summary>Category names accepted by unlock/lock/items (enum names plus German words).</summary>
    public static readonly IReadOnlyDictionary<string, ItemCategory> CategoryAliases =
        new Dictionary<string, ItemCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["cardback"] = ItemCategory.CardBack, ["back"] = ItemCategory.CardBack, ["rueckseite"] = ItemCategory.CardBack, ["rückseite"] = ItemCategory.CardBack,
            ["cardfront"] = ItemCategory.CardFront, ["front"] = ItemCategory.CardFront, ["deck"] = ItemCategory.CardFront,
            ["felt"] = ItemCategory.Felt, ["filz"] = ItemCategory.Felt,
            ["rail"] = ItemCategory.Rail, ["rand"] = ItemCategory.Rail,
            ["room"] = ItemCategory.Room, ["raum"] = ItemCategory.Room,
            ["charm"] = ItemCategory.Charm, ["gluecksbringer"] = ItemCategory.Charm, ["glücksbringer"] = ItemCategory.Charm,
            ["avatar"] = ItemCategory.Avatar,
            ["chips"] = ItemCategory.Chips,
        };

    /// <summary>Words for Tab completion after a command.</summary>
    public static IEnumerable<string> Completions(string command) => command.ToLowerInvariant() switch
    {
        "unlock" or "lock" => ["all", .. Enum.GetNames<ItemCategory>().Select(n => n.ToLowerInvariant()), .. ShopCatalog.All.Where(i => !i.IsDefault).Select(i => i.Id)],
        "equip" => ShopCatalog.All.Select(i => i.Id),
        "items" => Enum.GetNames<ItemCategory>().Select(n => n.ToLowerInvariant()),
        "achieve" or "unachieve" => ["all", .. Achievements.All.Select(a => a.Id)],
        "reset" => ["confirm"],
        _ => [],
    };

    public static DevResult Execute(PlayerState s, string line, bool german)
    {
        string T(string de, string en) => german ? de : en;
        var culture = CultureInfo.GetCultureInfo(german ? "de-DE" : "en-US");
        string N(int n) => n.ToString("N0", culture);
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return Fail(T("Kein Befehl.", "No command."));
        var name = parts[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        var b = s.Bankroll;
        var c = s.Collection;

        DevResult Changed(params string[] output)
        {
            c.MarkDevTools();
            return new DevResult(new OperationResult([]), output, true);
        }
        DevResult Info(IEnumerable<string> output) => new(new OperationResult([]), [.. output], true);
        DevResult Fail(string message) => new(new OperationResult([]), [message], false);

        switch (name)
        {
            case "chips":
            case "crowns":
            {
                if (args.Length != 1 || !TryAmount(args[0], name == "chips" ? b.Chips : b.Crowns, out var value))
                    return Fail(T($"Benutzung: {name} <Zahl | +Zahl | -Zahl>", $"Usage: {name} <number | +number | -number>"));
                var st = b.ToState();
                b.Restore(name == "chips" ? st with { Chips = value } : st with { Crowns = value });
                return name == "chips"
                    ? Changed(T($"Chips: {N(b.Chips)}", $"Chips: {N(b.Chips)}"))
                    : Changed(T($"Kronen: {N(b.Crowns)}", $"Crowns: {N(b.Crowns)}"));
            }

            case "unlock":
            case "lock":
            {
                if (args.Length == 0) return Fail(T($"Benutzung: {name} <all | Kategorie | Item-Id …>", $"Usage: {name} <all | category | item id …>"));
                var items = new List<ShopItem>();
                foreach (var arg in args)
                {
                    if (arg.Equals("all", StringComparison.OrdinalIgnoreCase)) items.AddRange(ShopCatalog.All);
                    else if (ParseCategory(arg) is { } cat) items.AddRange(ShopCatalog.All.Where(i => i.Category == cat));
                    else if (ShopCatalog.Find(arg) is { } item) items.Add(item);
                    else return Fail(T($"Unbekanntes Item oder Kategorie: {arg} (Liste: items)", $"Unknown item or category: {arg} (list: items)"));
                }
                var count = items.Where(i => !i.IsDefault).Distinct().Count(i => name == "unlock" ? c.Grant(i) : c.Revoke(i));
                return Changed(name == "unlock"
                    ? T($"{count} Item(s) freigeschaltet.", $"{count} item(s) unlocked.")
                    : T($"{count} Item(s) weggenommen.", $"{count} item(s) taken away."));
            }

            case "equip":
            {
                if (args.Length != 1 || ShopCatalog.Find(args[0]) is not { } item)
                    return Fail(T("Benutzung: equip <Item-Id> (Liste: items)", "Usage: equip <item id> (list: items)"));
                c.Grant(item);
                c.Equip(item);
                return Changed(T($"Angelegt: {item.Id} ({item.Category})", $"Equipped: {item.Id} ({item.Category})"));
            }

            case "items":
            {
                ItemCategory? filter = null;
                if (args.Length > 0)
                {
                    filter = ParseCategory(args[0]);
                    if (filter is null) return Fail(T($"Unbekannte Kategorie: {args[0]}", $"Unknown category: {args[0]}"));
                }
                var lines = new List<string>();
                foreach (var group in ShopCatalog.All.Where(i => filter is null || i.Category == filter).GroupBy(i => i.Category))
                {
                    lines.Add($"[{group.Key.ToString().ToLowerInvariant()}]");
                    lines.AddRange(group.Select(i =>
                        $"  {(c.IsEquipped(i) ? "★" : c.Owns(i) ? "✓" : "·")} {i.Id,-22} {(i.IsDefault ? T("Standard", "default") : $"{i.Price} 👑")}"));
                }
                lines.Add(T("★ angelegt  ✓ im Besitz  · nicht im Besitz", "★ equipped  ✓ owned  · not owned"));
                return Info(lines);
            }

            case "achieve":
            case "unachieve":
            {
                if (args.Length == 0) return Fail(T($"Benutzung: {name} <all | Erfolg-Id …>", $"Usage: {name} <all | achievement id …>"));
                var ids = new List<string>();
                foreach (var arg in args)
                {
                    if (arg.Equals("all", StringComparison.OrdinalIgnoreCase)) ids.AddRange(Achievements.All.Select(a => a.Id));
                    else if (Achievements.Find(arg) is { } a) ids.Add(a.Id);
                    else return Fail(T($"Unbekannter Erfolg: {arg} (Liste: achievements)", $"Unknown achievement: {arg} (list: achievements)"));
                }
                var count = ids.Distinct().Count(id => name == "achieve" ? c.Unlock(id) : c.Relock(id));
                return Changed(name == "achieve"
                    ? T($"{count} Erfolg(e) freigeschaltet.", $"{count} achievement(s) unlocked.")
                    : T($"{count} Erfolg(e) gesperrt.", $"{count} achievement(s) locked."));
            }

            case "achievements":
                return Info(Achievements.All.Select(a =>
                    $"  {(c.Achievements.Contains(a.Id) ? "✓" : "·")} {a.Icon} {a.Id,-20} +{a.Reward} 👑"));

            case "stats":
            {
                var st = b.ToState();
                return Info(
                [
                    $"  {T("Name", "Name")}: {s.DisplayName}",
                    $"  Chips: {N(st.Chips)} (+{N(st.TableChips)} {T("am Tisch", "on a table")})   {T("Kronen", "Crowns")}: {N(st.Crowns)}",
                    $"  Run #{st.Run}   {T("Rekord Run", "Run peak")}: {N(st.RunPeak)}   {T("Bester Rekord", "Best peak")}: {N(st.BestPeak)}",
                    $"  {T("Hände", "Hands")}: {N(st.HandsPlayed)} ({N(st.HandsWon)} {T("gewonnen", "won")})   {T("Größter Pot", "Biggest pot")}: {N(st.BiggestPot)}",
                    $"  {T("Tische geleert", "Tables cleared")}: {st.TablesCleared}   {T("Pleiten", "Busts")}: {st.Busts}",
                    $"  {T("Turniere", "Tournaments")}: {st.TournamentsPlayed} ({st.TournamentsWon} {T("gewonnen", "won")})",
                    $"  Blackjack: {N(st.BlackjackRounds)} {T("Runden", "rounds")} ({N(st.BlackjackWon)} {T("gewonnen", "won")}, {st.BlackjackNaturals}× Blackjack)   {T("Größter Gewinn", "Biggest win")}: {N(st.BlackjackBiggestWin)}",
                    $"  Items: {c.Owned.Count}/{ShopCatalog.All.Count(i => !i.IsDefault)}   {T("Erfolge", "Achievements")}: {Achievements.All.Count(a => c.Achievements.Contains(a.Id))}/{Achievements.All.Count}",
                    $"  {T("Dev-Befehle benutzt", "Developer commands used")}: {(c.UsedDevTools ? T("ja 🛠", "yes 🛠") : T("nein", "no"))}",
                ]);
            }

            case "reset":
            {
                if (args.Length != 1 || !args[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
                    return Fail(T("Setzt ALLES zurück. Zum Bestätigen: reset confirm", "Resets EVERYTHING. To confirm: reset confirm"));
                if (b.IsSeated) return Fail(T("Erst den Tisch verlassen.", "Leave the table first."));
                b.Restore(Bankroll.New().ToState());
                foreach (var item in ShopCatalog.All) c.Revoke(item);
                foreach (var a in Achievements.All) c.Relock(a.Id);
                return Changed(T($"Profil zurückgesetzt: {N(b.Chips)} Chips, 0 Kronen, keine Items.", $"Profile reset: {N(b.Chips)} chips, 0 crowns, no items."));
            }

            default:
                return Fail(T($"Unbekannter Befehl: {name} – „help“ zeigt alle Befehle.", $"Unknown command: {name} – \"help\" lists all commands."));
        }
    }

    private static ItemCategory? ParseCategory(string s) =>
        CategoryAliases.TryGetValue(s, out var cat) ? cat
        : Enum.TryParse<ItemCategory>(s, ignoreCase: true, out var parsed) && !int.TryParse(s, out _) ? parsed : null;

    /// <summary>"5000" sets, "+5000" / "-5000" change the current value. Accepts 5k / 2m.</summary>
    private static bool TryAmount(string text, int current, out int value)
    {
        value = 0;
        var sign = text[0] is '+' or '-' ? text[0] : '\0';
        var number = (sign == '\0' ? text : text[1..]).Replace(".", "").Replace("_", "").ToLowerInvariant();
        long factor = 1;
        if (number.EndsWith('k')) { factor = 1_000; number = number[..^1]; }
        else if (number.EndsWith('m')) { factor = 1_000_000; number = number[..^1]; }
        if (!long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return false;
        n *= factor;
        var result = sign switch { '+' => current + n, '-' => current - n, _ => n };
        value = (int)Math.Clamp(result, 0, MaxValue);
        return true;
    }
}
