namespace ChipRoyale.Client.Components.Game;

/// <summary>
/// Chip values and bet steps for the casino games. They grow with the stack, so that tables
/// without a limit (VIP) can be played with any amount in a few clicks.
/// </summary>
public static class BetSizes
{
    /// <summary>Casino chip values (1 up to one billion).</summary>
    public static readonly int[] Denominations =
    [
        1, 5, 10, 25, 50, 100, 250, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000,
        1_000_000, 2_500_000, 5_000_000, 10_000_000, 25_000_000, 50_000_000, 100_000_000, 250_000_000, 500_000_000, 1_000_000_000,
    ];

    /// <summary>
    /// Chip buttons: up to <paramref name="count"/> values between the minimum and the limit, the biggest
    /// one not above half the limit (so a few clicks make any bet).
    /// </summary>
    public static IReadOnlyList<int> Chips(int min, int limit, int count)
    {
        var all = Denominations.Where(v => v >= min && v <= Math.Max(min, limit)).ToList();
        if (all.Count == 0) return [min];
        if (all.Count <= count) return all;
        var top = all.FindLastIndex(v => v <= Math.Max(min, limit / 2));
        top = Math.Max(top, count - 1);
        return all.GetRange(top - count + 1, count);
    }

    /// <summary>Bet steps 1-2-5-10-… from the minimum up to <paramref name="max"/>.</summary>
    public static IReadOnlyList<int> Steps(int min, int max)
    {
        var steps = new List<int> { min };
        for (long decade = 1; decade <= int.MaxValue; decade *= 10)
        {
            foreach (var f in new[] { 1, 2, 5 })
            {
                var v = decade * f;
                if (v > min && v <= max && v <= int.MaxValue) steps.Add((int)v);
            }
        }
        return steps;
    }

    /// <summary>1.5k, 2M, 1B – short labels for chips.</summary>
    public static string Short(long v) => v switch
    {
        >= 1_000_000_000 => $"{v / 1_000_000_000.0:0.#}B",
        >= 1_000_000 => $"{v / 1_000_000.0:0.#}M",
        >= 1_000 => $"{v / 1_000.0:0.#}k",
        _ => v.ToString(),
    };

    public static string Color(long v) => v switch
    {
        >= 100_000_000 => "ruby",
        >= 10_000_000 => "navy",
        >= 1_000_000 => "teal",
        >= 100_000 => "pink",
        >= 10_000 => "blue",
        >= 5_000 => "orange",
        >= 1_000 => "gold",
        >= 500 => "purple",
        >= 100 => "black",
        >= 25 => "green",
        >= 5 => "red",
        _ => "white",
    };
}
