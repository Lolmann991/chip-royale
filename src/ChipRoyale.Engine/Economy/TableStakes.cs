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
}
