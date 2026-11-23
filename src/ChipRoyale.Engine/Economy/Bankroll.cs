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

    /// <summary>Shop currency. Survives going broke (like bought styles).</summary>
    public int Crowns { get; private set; }

    public int TournamentsPlayed { get; private set; }
    public int TournamentsWon { get; private set; }

    public int Total => Chips + TableChips;

    public bool IsSeated => TableChips > 0;

    /// <summary>Nothing on a table and not enough for the cheapest buy-in.</summary>
    public bool IsBroke => TableChips == 0 && Chips < TableStakes.SmallestMinBuyIn;

    /// <summary>Plain copy of all values (for JSON: the web API and the offline profile).</summary>
    public BankrollState ToState() => new(Chips, TableChips, Run, RunPeak, BestPeak, HandsPlayed, HandsWon, BiggestPot,
        Busts, TablesCleared, Crowns, TournamentsPlayed, TournamentsWon);

    public static Bankroll FromState(BankrollState s) => new()
    {
        Chips = s.Chips,
        TableChips = s.TableChips,
        Run = s.Run,
        RunPeak = s.RunPeak,
        BestPeak = s.BestPeak,
        HandsPlayed = s.HandsPlayed,
        HandsWon = s.HandsWon,
        BiggestPot = s.BiggestPot,
        Busts = s.Busts,
        TablesCleared = s.TablesCleared,
        Crowns = s.Crowns,
        TournamentsPlayed = s.TournamentsPlayed,
        TournamentsWon = s.TournamentsWon,
    };

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

    /// <summary>Chips per crown.</summary>
    public const int ExchangeRate = 100;

    /// <summary>Only chips above the starting chips can be exchanged (otherwise busting and resetting would farm crowns).</summary>
    public int ExchangeableChips => IsSeated ? 0 : Math.Max(0, Chips - TableStakes.StartingChips) / ExchangeRate * ExchangeRate;

    /// <summary>One-way exchange chips → crowns.</summary>
    public int Exchange(int chips)
    {
        if (chips <= 0 || chips % ExchangeRate != 0)
            throw new ArgumentException($"Exchange multiples of {ExchangeRate} chips.", nameof(chips));
        if (chips > ExchangeableChips)
            throw new InvalidOperationException("Not enough exchangeable chips.");
        Chips -= chips;
        var crowns = chips / ExchangeRate;
        Crowns += crowns;
        return crowns;
    }

    public void AddCrowns(int crowns) => Crowns += Math.Max(0, crowns);

    public void SpendCrowns(int crowns)
    {
        if (crowns < 0 || crowns > Crowns)
            throw new InvalidOperationException("Not enough crowns.");
        Crowns -= crowns;
    }

    public bool CanEnterTournament(int buyIn) => !IsSeated && Chips >= buyIn;

    /// <summary>Pays a tournament buy-in. It is gone; prizes come back via <see cref="RecordTournament"/>.</summary>
    public void PayTournamentBuyIn(int buyIn)
    {
        if (!CanEnterTournament(buyIn))
            throw new InvalidOperationException("Not enough chips for this tournament.");
        Chips -= buyIn;
    }

    /// <summary>Books a finished tournament: place (1 = winner) and prize money.</summary>
    public void RecordTournament(int place, int prize)
    {
        TournamentsPlayed++;
        if (place == 1) TournamentsWon++;
        Chips += Math.Max(0, prize);
        TrackPeak();
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

/// <summary>Serializable snapshot of a <see cref="Bankroll"/>.</summary>
public sealed record BankrollState(
    int Chips,
    int TableChips,
    int Run,
    int RunPeak,
    int BestPeak,
    int HandsPlayed,
    int HandsWon,
    int BiggestPot,
    int Busts,
    int TablesCleared,
    int Crowns,
    int TournamentsPlayed,
    int TournamentsWon);
