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

    public static readonly GameEvent None = new();
}

/// <param name="Reward">Crowns paid out when unlocked.</param>
public sealed record Achievement(string Id, string Icon, int Reward, Func<GameEvent, Bankroll, bool> Condition);

public static class Achievements
{
    public static readonly IReadOnlyList<Achievement> All =
    [
        new("first-win", "🥇", 2, (e, _) => e.HandWon),
        new("hands-100", "🃏", 5, (_, b) => b.HandsPlayed >= 100),
        new("hands-500", "🗂️", 15, (_, b) => b.HandsPlayed >= 500),
        new("full-house", "🏠", 3, (e, _) => e.HandWon && e.WinningHand >= HandCategory.FullHouse),
        new("quads", "🍀", 8, (e, _) => e.HandWon && e.WinningHand >= HandCategory.FourOfAKind),
        new("straight-flush", "🌈", 20, (e, _) => e.HandWon && e.WinningHand >= HandCategory.StraightFlush),
        new("big-pot", "💰", 5, (e, _) => e.HandWon && e.Pot >= 5000),
        new("table-cleared", "🧹", 10, (e, b) => e.TableCleared || b.TablesCleared > 0),
        new("sng-cash", "🎯", 5, (e, _) => e.TournamentPlace is 1 or 2),
        new("sng-win", "🏆", 15, (e, b) => e.TournamentPlace == 1 || b.TournamentsWon > 0),
        new("rich", "💎", 10, (_, b) => b.Total >= 10_000),
        new("student", "🎓", 5, (e, _) => e.LessonsCompleted),
        new("busted", "💸", 2, (_, b) => b.Busts > 0),
    ];

    public static Achievement? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    /// <summary>Unlocks every achievement whose condition is now met and pays its crowns.</summary>
    /// <returns>The newly unlocked achievements.</returns>
    public static IReadOnlyList<Achievement> Check(PlayerCollection collection, Bankroll bankroll, GameEvent e)
    {
        var unlocked = new List<Achievement>();
        foreach (var a in All)
        {
            if (collection.Achievements.Contains(a.Id) || !a.Condition(e, bankroll)) continue;
            collection.Unlock(a.Id);
            bankroll.AddCrowns(a.Reward);
            unlocked.Add(a);
        }
        return unlocked;
    }
}
