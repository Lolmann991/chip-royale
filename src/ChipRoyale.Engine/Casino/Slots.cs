namespace ChipRoyale.Engine.Casino;

public enum SlotSymbol
{
    Cherry,
    Lemon,
    Grape,
    Bell,
    Clover,
    Diamond,
    Seven,
    /// <summary>Replaces every symbol except the star.</summary>
    Wild,
    /// <summary>Pays anywhere on the screen; 3 or more start free spins.</summary>
    Star,
}

/// <param name="Line">Index of the payline (0–9).</param>
/// <param name="Count">Symbols in a row from the left (3–5).</param>
/// <param name="Multiplier">Win in line bets (the bet per spin is 10 line bets).</param>
public sealed record LineWin(int Line, SlotSymbol Symbol, int Count, int Multiplier);

/// <param name="Grid">[reel, row] – 5 reels, 3 rows.</param>
/// <param name="Win">Chips won (already doubled in free spins).</param>
public sealed record SlotSpin(
    SlotSymbol[,] Grid,
    IReadOnlyList<LineWin> Lines,
    int Stars,
    int ScatterMultiplier,
    int FreeSpinsWon,
    long Win,
    bool Jackpot)
{
    /// <summary>Positions [reel, row] that are part of a win (to light them up).</summary>
    public IEnumerable<(int Reel, int Row)> WinningCells()
    {
        foreach (var w in Lines)
            for (var r = 0; r < w.Count; r++)
                yield return (r, SlotMachine.Paylines[w.Line][r]);
        if (Stars >= 3)
            for (var reel = 0; reel < 5; reel++)
                for (var row = 0; row < 3; row++)
                    if (Grid[reel, row] == SlotSymbol.Star) yield return (reel, row);
    }
}

/// <summary>
/// Five reels, three rows, ten paylines. Wins pay left to right; stars pay anywhere and start
/// ten free spins (wins doubled). The return to player is about 95 % (see the tests).
/// </summary>
public sealed class SlotMachine
{
    /// <summary>Row on each reel (0 = top) for the ten lines.</summary>
    public static readonly IReadOnlyList<int[]> Paylines =
    [
        [1, 1, 1, 1, 1],
        [0, 0, 0, 0, 0],
        [2, 2, 2, 2, 2],
        [0, 1, 2, 1, 0],
        [2, 1, 0, 1, 2],
        [0, 0, 1, 2, 2],
        [2, 2, 1, 0, 0],
        [1, 0, 0, 0, 1],
        [1, 2, 2, 2, 1],
        [0, 1, 1, 1, 0],
    ];

    public const int LinesCount = 10;
    public const int FreeSpins = 10;

    /// <summary>Line pays for 3, 4 and 5 in a row (in line bets).</summary>
    public static readonly IReadOnlyDictionary<SlotSymbol, int[]> PayTable = new Dictionary<SlotSymbol, int[]>
    {
        [SlotSymbol.Cherry] = [5, 13, 40],
        [SlotSymbol.Lemon] = [5, 13, 40],
        [SlotSymbol.Grape] = [8, 26, 65],
        [SlotSymbol.Bell] = [10, 40, 100],
        [SlotSymbol.Clover] = [16, 65, 160],
        [SlotSymbol.Diamond] = [26, 100, 320],
        [SlotSymbol.Seven] = [50, 200, 1000],
        [SlotSymbol.Wild] = [30, 130, 650],
    };

    /// <summary>Scatter pays (×bet per spin) for 3, 4, 5 stars anywhere.</summary>
    public static readonly int[] StarPays = [2, 10, 50];

    /// <summary>How often each symbol is on a reel strip.</summary>
    private static readonly (SlotSymbol Symbol, int Count)[] StripMix =
    [
        (SlotSymbol.Cherry, 9), (SlotSymbol.Lemon, 9), (SlotSymbol.Grape, 7), (SlotSymbol.Bell, 6),
        (SlotSymbol.Clover, 5), (SlotSymbol.Diamond, 4), (SlotSymbol.Seven, 3), (SlotSymbol.Wild, 2), (SlotSymbol.Star, 2),
    ];

    /// <summary>The five reel strips (fixed order, like a real machine).</summary>
    public static readonly IReadOnlyList<SlotSymbol[]> Reels = BuildReels();

    private static SlotSymbol[][] BuildReels()
    {
        var reels = new SlotSymbol[5][];
        for (var r = 0; r < 5; r++)
        {
            var strip = StripMix.SelectMany(m => Enumerable.Repeat(m.Symbol, m.Count)).ToArray();
            var random = new Random(1000 + r);
            for (var i = strip.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (strip[i], strip[j]) = (strip[j], strip[i]);
            }
            reels[r] = strip;
        }
        return reels;
    }

    private readonly Random _random;

    public SlotMachine(Random? random = null) => _random = random ?? Random.Shared;

    /// <param name="bet">Bet per spin (10 lines); in free spins nothing is paid but the bet decides the wins.</param>
    /// <param name="freeSpin">Wins are doubled.</param>
    public SlotSpin Spin(int bet, bool freeSpin = false)
    {
        var grid = new SlotSymbol[5, 3];
        for (var reel = 0; reel < 5; reel++)
        {
            var strip = Reels[reel];
            var stop = _random.Next(strip.Length);
            for (var row = 0; row < 3; row++) grid[reel, row] = strip[(stop + row) % strip.Length];
        }
        return Evaluate(grid, bet, freeSpin);
    }

    /// <summary>Wins of a screen (public for tests and the tutorial).</summary>
    public static SlotSpin Evaluate(SlotSymbol[,] grid, int bet, bool freeSpin = false)
    {
        var lines = new List<LineWin>();
        for (var l = 0; l < Paylines.Count; l++)
        {
            if (LineWinOf(grid, l) is { } w) lines.Add(w);
        }

        var stars = 0;
        for (var reel = 0; reel < 5; reel++)
            for (var row = 0; row < 3; row++)
                if (grid[reel, row] == SlotSymbol.Star) stars++;
        var scatter = stars >= 3 ? StarPays[Math.Min(stars, 5) - 3] : 0;

        // Line bets are a tenth of the bet: sum all line multipliers first, round once.
        var lineTenths = lines.Sum(w => w.Multiplier);
        var win = (long)Math.Round(bet * (lineTenths / 10.0 + scatter), MidpointRounding.AwayFromZero);
        if (freeSpin) win *= 2;
        var jackpot = lines.Any(w => w.Symbol == SlotSymbol.Seven && w.Count == 5);
        return new SlotSpin(grid, lines, stars, scatter, stars >= 3 ? FreeSpins : 0, win, jackpot);
    }

    private static LineWin? LineWinOf(SlotSymbol[,] grid, int line)
    {
        var rows = Paylines[line];
        // The paying symbol is the first one that is not wild (all wild = wild pays).
        var symbol = SlotSymbol.Wild;
        for (var r = 0; r < 5; r++)
        {
            var s = grid[r, rows[r]];
            if (s == SlotSymbol.Star) break;
            if (s != SlotSymbol.Wild) { symbol = s; break; }
        }
        var count = 0;
        for (var r = 0; r < 5; r++)
        {
            var s = grid[r, rows[r]];
            if (s == symbol || (s == SlotSymbol.Wild && symbol != SlotSymbol.Star)) count++;
            else break;
        }
        if (count < 3 || !PayTable.TryGetValue(symbol, out var pays)) return null;

        // A line that starts with wilds may pay more as wilds than as the symbol.
        var wilds = 0;
        while (wilds < 5 && grid[wilds, rows[wilds]] == SlotSymbol.Wild) wilds++;
        var best = new LineWin(line, symbol, count, pays[count - 3]);
        if (wilds >= 3 && PayTable[SlotSymbol.Wild][wilds - 3] > best.Multiplier)
            best = new LineWin(line, SlotSymbol.Wild, wilds, PayTable[SlotSymbol.Wild][wilds - 3]);
        return best;
    }
}
