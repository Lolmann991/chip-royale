using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Economy;

/// <summary>Something that just happened; achievements are checked against it and the bankroll.</summary>
public sealed record GameEvent
{
    public bool HandWon { get; init; }
    public int Pot { get; init; }

    /// <summary>The winning hand if the player won at showdown.</summary>
    public HandCategory? WinningHand { get; init; }

    public bool TableCleared { get; init; }

    /// <summary>Final place of a finished Sit &amp; Go.</summary>
    public int? TournamentPlace { get; init; }

    public bool LessonsCompleted { get; init; }

    /// <summary>A blackjack round: natural 21, a doubled hand won, all split hands won.</summary>
    public bool BlackjackNatural { get; init; }
    public bool BlackjackDoubleWon { get; init; }
    public bool BlackjackSplitWon { get; init; }

    /// <summary>A casino round (roulette, slots, video poker) and its special results.</summary>
    public string? CasinoGame { get; init; }
    public IReadOnlyList<string> CasinoHighlights { get; init; } = [];
    public bool Has(string highlight) => CasinoHighlights.Contains(highlight);

    /// <summary>Days in a row the daily bonus was collected (when it was just collected).</summary>
    public int DailyStreak { get; init; }

    /// <summary>Different games the player has played (poker, blackjack, roulette, …).</summary>
    public int GamesPlayed { get; init; }

    public static readonly GameEvent None = new();
}

/// <param name="Reward">Crowns paid out when unlocked.</param>
public sealed record Achievement(string Id, string Icon, int Reward, Func<GameEvent, Bankroll, PlayerCollection, bool> Condition);

public static class Achievements
{
    public static readonly IReadOnlyList<Achievement> All =
    [
        new("first-win", "🥇", 2, (e, _, _) => e.HandWon),
        new("hands-100", "🃏", 5, (_, b, _) => b.HandsPlayed >= 100),
        new("hands-500", "🗂️", 15, (_, b, _) => b.HandsPlayed >= 500),
        new("full-house", "🏠", 3, (e, _, _) => e.HandWon && e.WinningHand >= HandCategory.FullHouse),
        new("quads", "🍀", 8, (e, _, _) => e.HandWon && e.WinningHand >= HandCategory.FourOfAKind),
        new("straight-flush", "🌈", 20, (e, _, _) => e.HandWon && e.WinningHand >= HandCategory.StraightFlush),
        new("big-pot", "💰", 5, (e, _, _) => e.HandWon && e.Pot >= 5000),
        new("table-cleared", "🧹", 10, (e, b, _) => e.TableCleared || b.TablesCleared > 0),
        new("sng-cash", "🎯", 5, (e, _, _) => e.TournamentPlace is 1 or 2),
        new("sng-win", "🏆", 15, (e, b, _) => e.TournamentPlace == 1 || b.TournamentsWon > 0),
        new("rich", "💎", 10, (_, b, _) => b.Total >= 10_000),
        new("student", "🎓", 5, (e, _, _) => e.LessonsCompleted),
        new("busted", "💸", 2, (_, b, _) => b.Busts > 0),
        new("collector", "🧺", 10, (_, _, c) => c.Owned.Count >= 10),
        new("trendsetter", "✨", 15, (_, _, c) => Enum.GetValues<ItemCategory>().All(cat => !c.Equipped(cat).IsDefault)),
        new("bj-natural", "🂡", 3, (e, b, _) => e.BlackjackNatural || b.BlackjackNaturals > 0),
        new("bj-double", "✌️", 3, (e, _, _) => e.BlackjackDoubleWon),
        new("bj-split", "🪓", 5, (e, _, _) => e.BlackjackSplitWon),
        new("bj-rounds-100", "🃁", 8, (_, b, _) => b.BlackjackRounds >= 100),
        new("roulette-straight", "🎯", 5, (e, _, _) => e.CasinoGame == CasinoTables.Roulette && e.Has("straight")),
        new("roulette-zero", "🟢", 5, (e, _, _) => e.CasinoGame == CasinoTables.Roulette && e.Has("zero")),
        new("slots-freespins", "🌟", 3, (e, _, _) => e.CasinoGame == CasinoTables.Slots && e.Has("freespins")),
        new("slots-bigwin", "💥", 5, (e, _, _) => e.CasinoGame == CasinoTables.Slots && e.Has("bigwin")),
        new("slots-jackpot", "🎰", 20, (e, _, _) => e.CasinoGame == CasinoTables.Slots && e.Has("jackpot")),
        new("vp-quads", "🎲", 5, (e, _, _) => e.CasinoGame == CasinoTables.VideoPoker && e.Has("quads")),
        new("vp-royal", "👑", 25, (e, _, _) => e.CasinoGame == CasinoTables.VideoPoker && e.Has("royal")),
        new("daily-7", "📅", 10, (e, _, _) => e.DailyStreak >= 7),
        new("allrounder", "🎪", 10, (e, _, _) => e.GamesPlayed >= 5),
    ];

    public static Achievement? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    /// <summary>Unlocks every achievement whose condition is now met and pays its crowns.</summary>
    /// <returns>The newly unlocked achievements.</returns>
    public static IReadOnlyList<Achievement> Check(PlayerCollection collection, Bankroll bankroll, GameEvent e)
    {
        var unlocked = new List<Achievement>();
        foreach (var a in All)
        {
            if (collection.Achievements.Contains(a.Id) || !a.Condition(e, bankroll, collection)) continue;
            collection.Unlock(a.Id);
            bankroll.AddCrowns(a.Reward);
            unlocked.Add(a);
        }
        return unlocked;
    }
}
