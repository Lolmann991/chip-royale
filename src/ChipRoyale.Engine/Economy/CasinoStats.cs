using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChipRoyale.Engine.Economy;

/// <summary>Statistics of one casino game (roulette, slots, video poker, …).</summary>
public sealed class GameStat
{
    public int Rounds { get; set; }
    public int Won { get; set; }

    /// <summary>All bets added up.</summary>
    public long Wagered { get; set; }

    /// <summary>Net result over all rounds (won minus lost).</summary>
    public long Net { get; set; }

    /// <summary>Biggest net win of one round.</summary>
    public int Biggest { get; set; }

    /// <summary>Special results, e.g. "jackpot" → 2.</summary>
    public Dictionary<string, int> Highlights { get; set; } = [];
}

/// <summary>
/// Casino statistics and the daily bonus. Stored as one JSON text (database column / browser
/// profile), so new games need no database change.
/// </summary>
public sealed class CasinoStats
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public Dictionary<string, GameStat> Games { get; set; } = [];

    /// <summary>Day the daily bonus was last collected (yyyy-MM-dd).</summary>
    public string? LastDaily { get; set; }

    /// <summary>Days in a row the bonus was collected.</summary>
    public int DailyStreak { get; set; }

    public GameStat Get(string game) => Games.TryGetValue(game, out var s) ? s : new GameStat();

    /// <param name="highlights">Special results of the round (e.g. "jackpot").</param>
    public void Record(string game, int bet, int net, IEnumerable<string> highlights)
    {
        if (!Games.TryGetValue(game, out var s)) Games[game] = s = new GameStat();
        s.Rounds++;
        if (net > 0) s.Won++;
        s.Wagered += Math.Max(0, bet);
        s.Net += net;
        s.Biggest = Math.Max(s.Biggest, net);
        foreach (var h in highlights.Where(h => !string.IsNullOrEmpty(h)).Distinct())
            s.Highlights[h] = s.Highlights.GetValueOrDefault(h) + 1;
    }

    public int HighlightCount(string game, string highlight) => Get(game).Highlights.GetValueOrDefault(highlight);

    // ------------------------------------------------------------------ daily bonus

    public bool DailyAvailable(DateOnly today) => LastDaily != Format(today);

    /// <summary>Streak the player gets today (yesterday collected → +1, otherwise 1).</summary>
    public int StreakFor(DateOnly today) => LastDaily == Format(today.AddDays(-1)) ? DailyStreak + 1 : 1;

    public void CollectDaily(DateOnly today)
    {
        DailyStreak = StreakFor(today);
        LastDaily = Format(today);
    }

    private static string Format(DateOnly d) => d.ToString("yyyy-MM-dd");

    // ------------------------------------------------------------------ storage

    public string Serialize() => Games.Count == 0 && LastDaily is null ? "" : JsonSerializer.Serialize(this, Json);

    public static CasinoStats Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new CasinoStats();
        try
        {
            return JsonSerializer.Deserialize<CasinoStats>(json, Json) ?? new CasinoStats();
        }
        catch (JsonException)
        {
            return new CasinoStats();
        }
    }
}

/// <summary>The daily bonus wheel: segments with chip amounts and how often they come up.</summary>
public static class DailyWheel
{
    public static readonly IReadOnlyList<(int Chips, int Weight)> Segments =
    [
        (100, 18), (250, 14), (150, 18), (500, 8), (200, 16), (1000, 3), (300, 12), (2500, 1),
    ];

    /// <summary>+10 % per day in a row, at most +100 % (day 11).</summary>
    public static double StreakBonus(int streak) => Math.Min(1.0, Math.Max(0, streak - 1) * 0.1);

    public static int Spin(Random random)
    {
        var total = Segments.Sum(s => s.Weight);
        var roll = random.Next(total);
        for (var i = 0; i < Segments.Count; i++)
        {
            roll -= Segments[i].Weight;
            if (roll < 0) return i;
        }
        return 0;
    }

    public static int Payout(int segment, int streak) => (int)Math.Round(Segments[segment].Chips * (1 + StreakBonus(streak)));
}
