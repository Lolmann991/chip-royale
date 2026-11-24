namespace ChipRoyale.Engine.Economy;

/// <summary>A table level. Buy-in range is <see cref="MinBuyIn"/> to 20 × <see cref="MinBuyIn"/>.</summary>
public sealed record TableStake(string Id, string Name, int SmallBlind, int BigBlind, int MinBuyIn, bool IsPractice = false)
{
    public const int MaxBuyInFactor = 20;

    public int MaxBuyIn => MinBuyIn * MaxBuyInFactor;
}

public static class TableStakes
{
    /// <summary>Chips every new run starts with.</summary>
    public const int StartingChips = 1000;

    /// <summary>Free table against very easy bots; nothing is won or lost.</summary>
    public static readonly TableStake Practice = new("practice", "Übungstisch", 5, 10, 1000, IsPractice: true);

    public static readonly IReadOnlyList<TableStake> All =
    [
        Practice,
        new("pub", "Kneipe", 5, 10, 200),
        new("casino", "Casino", 25, 50, 1000),
        new("highroller", "High Roller", 100, 200, 4000),
        new("vip", "VIP-Salon", 500, 1000, 20000),
    ];

    public static IEnumerable<TableStake> Real => All.Where(s => !s.IsPractice);

    /// <summary>Below this a player cannot sit anywhere and counts as broke.</summary>
    public static int SmallestMinBuyIn => Real.Min(s => s.MinBuyIn);

    public static TableStake? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>Poker or blackjack table.</summary>
    public static TableStake? FindAny(string id) => Find(id) ?? BlackjackTables.Find(id) ?? CasinoTables.Find(id);
}

/// <summary>
/// Blackjack tables. <see cref="TableStake.SmallBlind"/> is the smallest bet, <see cref="TableStake.BigBlind"/>
/// the biggest; the buy-in works like at the poker tables.
/// </summary>
public static class BlackjackTables
{
    public static readonly TableStake Practice = new("bj-practice", "Übungstisch", 10, 200, 1000, IsPractice: true);

    public static readonly IReadOnlyList<TableStake> All =
    [
        Practice,
        new("bj-pub", "Kneipe", 5, 100, 100),
        new("bj-casino", "Casino", 25, 500, 500),
        new("bj-vip", "VIP-Salon", 100, 2000, 2000),
    ];

    public static TableStake? Find(string id) => All.FirstOrDefault(s => s.Id == id);
}

/// <summary>
/// Tables (machines) of the other casino games: roulette, slots, video poker. Id "&lt;game&gt;-&lt;level&gt;";
/// <see cref="TableStake.SmallBlind"/> is the smallest bet, <see cref="TableStake.BigBlind"/> the biggest.
/// </summary>
public static class CasinoTables
{
    public const string Roulette = "roulette";
    public const string Slots = "slots";
    public const string VideoPoker = "videopoker";

    public static readonly IReadOnlyList<string> Games = [Roulette, Slots, VideoPoker];

    private static readonly (string Level, int MinBet, int MaxBet, int MinBuyIn, bool Practice)[] Levels =
    [
        ("practice", 1, 100, 1000, true),
        ("pub", 1, 50, 50, false),
        ("casino", 5, 250, 250, false),
        ("vip", 25, 1000, 1000, false),
    ];

    public static readonly IReadOnlyList<TableStake> All =
    [
        .. Games.SelectMany(g => Levels.Select(l => new TableStake($"{g}-{l.Level}", l.Level, l.MinBet, l.MaxBet, l.MinBuyIn, l.Practice))),
    ];

    public static IEnumerable<TableStake> For(string game) => All.Where(t => t.Id.StartsWith(game + "-", StringComparison.Ordinal));

    public static TableStake? Find(string id) => All.FirstOrDefault(s => s.Id == id);

    /// <summary>"pub", "casino", … of a table.</summary>
    public static string Level(TableStake t) => t.Name;
}
