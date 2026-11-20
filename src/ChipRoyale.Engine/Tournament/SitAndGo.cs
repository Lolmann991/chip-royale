using ChipRoyale.Engine.Holdem;

namespace ChipRoyale.Engine.Tournament;

public sealed record BlindLevel(int SmallBlind, int BigBlind);

/// <summary>Rules of a Sit &amp; Go: one table, everybody starts with the same stack, blinds rise, top places are paid.</summary>
public sealed record SitAndGoConfig(
    string Id,
    int BuyIn,
    int Players = 6,
    int StartingStack = 1500,
    int HandsPerLevel = 8)
{
    public IReadOnlyList<BlindLevel> Levels { get; init; } = SitAndGoTiers.DefaultLevels;

    /// <summary>Share of the prize pool per place (1st, 2nd, ...).</summary>
    public IReadOnlyList<double> Payouts { get; init; } = [0.65, 0.35];

    /// <summary>All buy-ins together (the bots "pay" too).</summary>
    public int PrizePool => BuyIn * Players;

    /// <summary>Prize for a place (1-based). The last paid place gets the rounding remainder.</summary>
    public int Prize(int place)
    {
        if (place < 1 || place > Payouts.Count) return 0;
        if (place == Payouts.Count)
            return PrizePool - Enumerable.Range(1, Payouts.Count - 1).Sum(Prize);
        return (int)Math.Round(PrizePool * Payouts[place - 1]);
    }
}

public static class SitAndGoTiers
{
    public static readonly IReadOnlyList<BlindLevel> DefaultLevels =
    [
        new(10, 20), new(15, 30), new(25, 50), new(50, 100), new(75, 150),
        new(100, 200), new(150, 300), new(200, 400), new(300, 600), new(500, 1000),
    ];

    public static readonly IReadOnlyList<SitAndGoConfig> All =
    [
        new("bronze", 200),
        new("silver", 1000),
        new("gold", 5000),
    ];

    public static SitAndGoConfig? Find(string id) => All.FirstOrDefault(t => t.Id == id);
}

/// <summary>
/// Runs a Sit &amp; Go on top of a <see cref="HoldemTable"/>: call <see cref="BeforeHand"/> before
/// every <see cref="HoldemTable.StartHand"/> and <see cref="AfterHand"/> once a hand is complete.
/// </summary>
public sealed class SitAndGo
{
    private readonly HoldemTable _table;
    private readonly Dictionary<int, int> _places = new();
    private int[] _stacksAtStart = [];

    public SitAndGo(SitAndGoConfig config, HoldemTable table)
    {
        if (table.Players.Count != config.Players)
            throw new ArgumentException("Player count does not match the tournament.", nameof(table));
        Config = config;
        _table = table;
    }

    public SitAndGoConfig Config { get; }

    /// <summary>Hands started so far.</summary>
    public int HandsPlayed { get; private set; }

    /// <summary>0-based index into <see cref="SitAndGoConfig.Levels"/> of the current (or first) hand.</summary>
    public int Level => LevelOfHand(Math.Max(0, HandsPlayed - 1));

    public BlindLevel Blinds => Config.Levels[Level];

    /// <summary>Hands left at this level including the current one, or null on the last level.</summary>
    public int? HandsLeftInLevel => Level >= Config.Levels.Count - 1
        ? null
        : Config.HandsPerLevel - Math.Max(0, HandsPlayed - 1) % Config.HandsPerLevel;

    public int PlayersLeft => _table.Players.Count(p => p.Chips > 0);

    public bool IsFinished => PlayersLeft <= 1;

    /// <summary>Final places of knocked-out players (and the winner once finished).</summary>
    public IReadOnlyDictionary<int, int> Places => _places;

    public int? PlaceOf(int playerIndex) => _places.TryGetValue(playerIndex, out var p) ? p : null;

    /// <summary>Sets the blinds for the coming hand.</summary>
    /// <returns>True if a new blind level starts with this hand.</returns>
    public bool BeforeHand()
    {
        _stacksAtStart = _table.Players.Select(p => p.Chips).ToArray();
        var level = LevelOfHand(HandsPlayed);
        var levelUp = HandsPlayed > 0 && level != LevelOfHand(HandsPlayed - 1);
        _table.SetBlinds(Config.Levels[level].SmallBlind, Config.Levels[level].BigBlind);
        HandsPlayed++;
        return levelUp;
    }

    private int LevelOfHand(int handIndex) => Math.Min(handIndex / Config.HandsPerLevel, Config.Levels.Count - 1);

    /// <summary>Records who was knocked out in the last hand.</summary>
    /// <returns>Indices of the players knocked out in this hand.</returns>
    public IReadOnlyList<int> AfterHand()
    {
        var busted = _table.Players
            .Select((p, i) => (p, i))
            .Where(x => x.p.Chips == 0 && !_places.ContainsKey(x.i))
            .Select(x => x.i)
            .ToList();

        // Places counted from the bottom; whoever started the hand with more chips finishes higher.
        var nextPlace = PlayersLeft + busted.Count;
        foreach (var i in busted.OrderBy(i => _stacksAtStart.Length > i ? _stacksAtStart[i] : 0))
            _places[i] = nextPlace--;

        if (IsFinished)
        {
            var winner = _table.Players.Select((p, i) => (p, i)).FirstOrDefault(x => x.p.Chips > 0);
            if (winner.p is not null) _places[winner.i] = 1;
        }
        return busted;
    }
}
