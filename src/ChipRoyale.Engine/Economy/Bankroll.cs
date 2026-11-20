namespace ChipRoyale.Engine.Economy;

/// <summary>
/// A player's chips and records. Chips are either behind (<see cref="Chips"/>) or on a
/// table (<see cref="TableChips"/>). Going broke resets to the starting chips and starts
/// a new leaderboard run.
/// </summary>
public sealed class Bankroll
{
    public int Chips { get; private set; }
    public int TableChips { get; private set; }

    /// <summary>Number of the current run (1 = first run, +1 after every bust).</summary>
    public int Run { get; private set; } = 1;

    /// <summary>Highest total chips in the current run.</summary>
    public int RunPeak { get; private set; }

    /// <summary>Highest total chips of any run (leaderboard value).</summary>
    public int BestPeak { get; private set; }

    public int HandsPlayed { get; private set; }
    public int HandsWon { get; private set; }
    public int BiggestPot { get; private set; }
    public int Busts { get; private set; }

    /// <summary>Times the player knocked out every bot at a table.</summary>
    public int TablesCleared { get; private set; }

    public int Total => Chips + TableChips;

    public bool IsSeated => TableChips > 0;

    /// <summary>Nothing on a table and not enough for the cheapest buy-in.</summary>
    public bool IsBroke => TableChips == 0 && Chips < TableStakes.SmallestMinBuyIn;

    public static Bankroll New(int startingChips = TableStakes.StartingChips) => new()
    {
        Chips = startingChips,
        RunPeak = startingChips,
        BestPeak = startingChips,
    };

    public bool CanSit(TableStake stake) => stake.IsPractice || (!IsSeated && Chips >= stake.MinBuyIn);

    public int MaxBuyInFor(TableStake stake) => Math.Min(stake.MaxBuyIn, Chips);

    public void BuyIn(TableStake stake, int amount)
    {
        if (stake.IsPractice) return;
        if (IsSeated) throw new InvalidOperationException("Already sitting at a table.");
        if (amount < stake.MinBuyIn || amount > MaxBuyInFor(stake))
            throw new ArgumentOutOfRangeException(nameof(amount), $"Buy-in must be between {stake.MinBuyIn} and {MaxBuyInFor(stake)}.");
        Chips -= amount;
        TableChips = amount;
    }

    /// <summary>Stores the player's current stack at the table (call after every hand / investment).</summary>
    public void SetTableStack(int stack)
    {
        TableChips = Math.Max(0, stack);
        TrackPeak();
    }

    public void RecordHand(bool won, int pot)
    {
        HandsPlayed++;
        if (won) HandsWon++;
        BiggestPot = Math.Max(BiggestPot, pot);
    }

    public void RecordTableCleared() => TablesCleared++;

    /// <summary>Leaves the table and takes the stack back.</summary>
    public void CashOut()
    {
        Chips += TableChips;
        TableChips = 0;
        TrackPeak();
    }

    /// <summary>When broke: reset to the starting chips and begin a new run.</summary>
    /// <returns>True if a new run was started.</returns>
    public bool ResetIfBroke(int startingChips = TableStakes.StartingChips)
    {
        if (!IsBroke) return false;
        Busts++;
        Run++;
        Chips = startingChips;
        TableChips = 0;
        RunPeak = startingChips;
        return true;
    }

    private void TrackPeak()
    {
        RunPeak = Math.Max(RunPeak, Total);
        BestPeak = Math.Max(BestPeak, RunPeak);
    }
}
