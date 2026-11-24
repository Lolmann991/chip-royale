namespace ChipRoyale.Engine.Casino;

public enum RouletteBetKind
{
    /// <summary>One number (0–36): pays 35 to 1.</summary>
    Straight,
    /// <summary>Two neighbouring numbers on the layout: 17 to 1.</summary>
    Split,
    /// <summary>A row of three (1-2-3, 4-5-6, …): 11 to 1.</summary>
    Street,
    /// <summary>Four numbers meeting in a corner: 8 to 1.</summary>
    Corner,
    /// <summary>Two rows, six numbers: 5 to 1.</summary>
    SixLine,
    /// <summary>1–12, 13–24, 25–36: 2 to 1.</summary>
    Dozen,
    /// <summary>The three columns of the layout: 2 to 1.</summary>
    Column,
    Red,
    Black,
    Odd,
    Even,
    /// <summary>1–18.</summary>
    Low,
    /// <summary>19–36.</summary>
    High,
}

/// <summary>A bet on the layout. <see cref="Numbers"/> are the numbers it covers.</summary>
public sealed record RouletteBet(RouletteBetKind Kind, IReadOnlyList<int> Numbers, int Amount)
{
    /// <summary>Winnings per chip (the stake comes back on top).</summary>
    public int PayoutFactor => European.PayoutFactor(Kind);

    public bool Covers(int number) => Numbers.Contains(number);

    /// <summary>Same spot on the layout (for stacking chips).</summary>
    public string Key => $"{Kind}:{string.Join('-', Numbers)}";
}

/// <summary>European roulette: one zero, 37 pockets, house edge 1/37 ≈ 2.7 %.</summary>
public static class European
{
    /// <summary>Pockets clockwise around the wheel, starting with zero.</summary>
    public static readonly IReadOnlyList<int> WheelOrder =
    [
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
        5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
    ];

    private static readonly HashSet<int> RedNumbers = [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    public static bool IsRed(int n) => RedNumbers.Contains(n);
    public static bool IsBlack(int n) => n != 0 && !IsRed(n);

    /// <summary>Column 1 = 1, 4, 7 … 34 (bottom row of the layout), column 3 = 3, 6 … 36 (top row).</summary>
    public static int ColumnOf(int n) => n == 0 ? 0 : (n - 1) % 3 + 1;

    public static int DozenOf(int n) => n == 0 ? 0 : (n - 1) / 12 + 1;

    public static int PayoutFactor(RouletteBetKind kind) => kind switch
    {
        RouletteBetKind.Straight => 35,
        RouletteBetKind.Split => 17,
        RouletteBetKind.Street => 11,
        RouletteBetKind.Corner => 8,
        RouletteBetKind.SixLine => 5,
        RouletteBetKind.Dozen or RouletteBetKind.Column => 2,
        _ => 1,
    };

    // ------------------------------------------------------------------ building bets

    public static RouletteBet Straight(int n, int amount) =>
        n is < 0 or > 36 ? throw new ArgumentOutOfRangeException(nameof(n)) : new(RouletteBetKind.Straight, [n], amount);

    /// <summary>Two numbers next to each other on the layout (or 0 with 1, 2 or 3).</summary>
    public static RouletteBet Split(int a, int b, int amount)
    {
        var (lo, hi) = a < b ? (a, b) : (b, a);
        var ok = lo == 0 ? hi is >= 1 and <= 3 : hi - lo == 3 || (hi - lo == 1 && ColumnOf(lo) != 3);
        if (!ok || hi > 36) throw new ArgumentException("Not neighbours on the layout.");
        return new(RouletteBetKind.Split, [lo, hi], amount);
    }

    /// <param name="row">1 = 1-2-3, … 12 = 34-35-36.</param>
    public static RouletteBet Street(int row, int amount) =>
        row is < 1 or > 12 ? throw new ArgumentOutOfRangeException(nameof(row))
            : new(RouletteBetKind.Street, [3 * row - 2, 3 * row - 1, 3 * row], amount);

    /// <param name="low">Smallest number of the corner (its column must not be 3).</param>
    public static RouletteBet Corner(int low, int amount) =>
        low < 1 || low > 32 || ColumnOf(low) == 3 ? throw new ArgumentOutOfRangeException(nameof(low))
            : new(RouletteBetKind.Corner, [low, low + 1, low + 3, low + 4], amount);

    /// <param name="row">First of the two rows (1–11).</param>
    public static RouletteBet SixLine(int row, int amount) =>
        row is < 1 or > 11 ? throw new ArgumentOutOfRangeException(nameof(row))
            : new(RouletteBetKind.SixLine, [.. Enumerable.Range(3 * row - 2, 6)], amount);

    public static RouletteBet Dozen(int dozen, int amount) =>
        dozen is < 1 or > 3 ? throw new ArgumentOutOfRangeException(nameof(dozen))
            : new(RouletteBetKind.Dozen, [.. Enumerable.Range(12 * dozen - 11, 12)], amount);

    public static RouletteBet Column(int column, int amount) =>
        column is < 1 or > 3 ? throw new ArgumentOutOfRangeException(nameof(column))
            : new(RouletteBetKind.Column, [.. Enumerable.Range(1, 36).Where(n => ColumnOf(n) == column)], amount);

    public static RouletteBet Outside(RouletteBetKind kind, int amount) => new(kind, [.. Enumerable.Range(1, 36).Where(n => kind switch
    {
        RouletteBetKind.Red => IsRed(n),
        RouletteBetKind.Black => IsBlack(n),
        RouletteBetKind.Odd => n % 2 == 1,
        RouletteBetKind.Even => n % 2 == 0,
        RouletteBetKind.Low => n <= 18,
        RouletteBetKind.High => n >= 19,
        _ => throw new ArgumentException("Not an outside bet.", nameof(kind)),
    })], amount);

    // ------------------------------------------------------------------ playing

    public static int Spin(Random random) => random.Next(37);

    /// <summary>Chips paid back for these bets (winning stakes included).</summary>
    public static long Payout(IEnumerable<RouletteBet> bets, int number) =>
        bets.Where(b => b.Covers(number)).Sum(b => (long)b.Amount * (b.PayoutFactor + 1));

    /// <summary>Chance that at least one of the bets wins.</summary>
    public static double WinChance(IEnumerable<RouletteBet> bets) =>
        bets.SelectMany(b => b.Numbers).Distinct().Count() / 37.0;

    /// <summary>Highest possible payout of these bets over all numbers.</summary>
    public static long BestCase(IReadOnlyCollection<RouletteBet> bets) =>
        bets.Count == 0 ? 0 : Enumerable.Range(0, 37).Max(n => Payout(bets, n));
}
