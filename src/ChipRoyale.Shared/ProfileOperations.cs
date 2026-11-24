using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Tournament;

namespace ChipRoyale.Shared;

/// <summary>A player's game data while an operation runs on it.</summary>
public sealed class PlayerState(string displayName, Bankroll bankroll, PlayerCollection collection, CasinoStats? casino = null)
{
    public string DisplayName { get; set; } = displayName;
    public Bankroll Bankroll { get; } = bankroll;
    public PlayerCollection Collection { get; } = collection;
    public CasinoStats Casino { get; } = casino ?? new CasinoStats();

    public ProfileDto ToDto() => ProfileDto.From(DisplayName, Bankroll, Collection, Casino);

    public static PlayerState New(string displayName) => new(displayName, Bankroll.New(), new PlayerCollection());

    public static PlayerState FromDto(ProfileDto dto) =>
        new(dto.DisplayName, Bankroll.FromState(dto.Bankroll), dto.ToCollection(), CasinoStats.Parse(dto.CasinoStats));

    /// <summary>Different games played so far (poker, blackjack and every casino game).</summary>
    public int GamesPlayed =>
        (Bankroll.HandsPlayed > 0 || Bankroll.TournamentsPlayed > 0 ? 1 : 0)
        + (Bankroll.BlackjackRounds > 0 ? 1 : 0)
        + Casino.Games.Count(g => g.Value.Rounds > 0);
}

/// <param name="Unlocked">Achievement ids unlocked by the operation.</param>
/// <param name="Error">Resource key of an error (the operation did nothing).</param>
/// <param name="Value">Extra result of the operation (daily bonus: the wheel segment).</param>
public sealed record OperationResult(string[] Unlocked, bool StartedNewRun = false, int ReturnedFromTable = 0, string? Error = null, int? Value = null)
{
    public ProfileResponse ToResponse(PlayerState state) => new(state.ToDto(), Unlocked, StartedNewRun, ReturnedFromTable, Error, Value);

    public static OperationResult Fail(string errorKey) => new([], Error: errorKey);
}

/// <summary>
/// All changes to a player's chips, crowns and items. Used by the server (stored in the database)
/// and by the client's offline profile (stored in the browser), so both follow the same rules.
/// </summary>
public static class ProfileOperations
{
    /// <summary>On every start: chips left on a table (closed tab) are cashed out; broke players start a new run.</summary>
    public static OperationResult Load(PlayerState s)
    {
        var returned = 0;
        if (s.Bankroll.IsSeated)
        {
            returned = s.Bankroll.TableChips;
            s.Bankroll.CashOut();
        }
        var reset = s.Bankroll.ResetIfBroke();
        return new OperationResult([], reset, returned);
    }

    public static OperationResult BuyIn(PlayerState s, string stakeId, int amount)
    {
        if (TableStakes.FindAny(stakeId) is not { } stake) return OperationResult.Fail("Error.Request");
        if (!s.Bankroll.CanSit(stake) || (!stake.IsPractice && (amount < stake.MinBuyIn || amount > s.Bankroll.MaxBuyInFor(stake))))
            return OperationResult.Fail("Lobby.NotEnough");
        return Run(s, (b, _) => b.BuyIn(stake, amount));
    }

    public static OperationResult Table(PlayerState s, int stack, HandOutcomeDto? hand) => Run(s, (b, _) =>
    {
        b.SetTableStack(stack);
        if (hand is not null)
        {
            b.RecordHand(hand.Won, hand.Pot);
            if (hand.TableCleared) b.RecordTableCleared();
        }
    }, hand?.ToEvent());

    /// <summary>A blackjack round is over: new stack at the table and statistics.</summary>
    public static OperationResult Blackjack(PlayerState s, int stack, BlackjackRoundDto round) => Run(s, (b, _) =>
    {
        b.SetTableStack(stack);
        b.RecordBlackjack(round.Net, round.Natural);
    }, round.ToEvent());

    /// <summary>A round of roulette, slots or video poker is over: stack at the table and statistics.</summary>
    public static OperationResult CasinoRound(PlayerState s, int stack, CasinoRoundDto round)
    {
        if (!CasinoTables.Games.Contains(round.Game)) return OperationResult.Fail("Error.Request");
        var highlights = (round.Highlights ?? []).Where(h => h.Length is > 0 and <= 20).Take(5).ToArray();
        return Run(s, (b, _) =>
        {
            b.SetTableStack(stack);
            s.Casino.Record(round.Game, round.Bet, round.Net, highlights);
        }, () => new GameEvent { CasinoGame = round.Game, CasinoHighlights = highlights, GamesPlayed = s.GamesPlayed });
    }

    /// <summary>
    /// The daily bonus wheel (once per day): chips by the wheel segment, more for days in a row.
    /// <see cref="OperationResult.Value"/> is the segment, so the wheel can stop there.
    /// </summary>
    public static OperationResult DailyBonus(PlayerState s, DateOnly today, Random random)
    {
        if (!s.Casino.DailyAvailable(today)) return OperationResult.Fail("Daily.Taken");
        var segment = DailyWheel.Spin(random);
        var streak = s.Casino.StreakFor(today);
        var result = Run(s, (b, _) =>
        {
            s.Casino.CollectDaily(today);
            b.AddChips(DailyWheel.Payout(segment, streak));
        }, () => new GameEvent { DailyStreak = streak });
        return result with { Value = segment };
    }

    public static OperationResult CashOut(PlayerState s, int stack)
    {
        var reset = false;
        var r = Run(s, (b, _) =>
        {
            b.SetTableStack(stack);
            b.CashOut();
            reset = b.ResetIfBroke();
        });
        return r with { StartedNewRun = reset };
    }

    public static OperationResult TournamentEnter(PlayerState s, string tierId)
    {
        if (SitAndGoTiers.Find(tierId) is not { } tier) return OperationResult.Fail("Error.Request");
        if (!s.Bankroll.CanEnterTournament(tier.BuyIn)) return OperationResult.Fail("Lobby.NotEnough");
        return Run(s, (b, _) => b.PayTournamentBuyIn(tier.BuyIn));
    }

    /// <summary>The prize is computed here from tier and place, not taken from the caller.</summary>
    public static OperationResult TournamentFinish(PlayerState s, string tierId, int place)
    {
        if (SitAndGoTiers.Find(tierId) is not { } tier) return OperationResult.Fail("Error.Request");
        place = Math.Clamp(place, 1, tier.Players);
        var reset = false;
        var r = Run(s, (b, _) =>
        {
            b.RecordTournament(place, tier.Prize(place));
            reset = b.ResetIfBroke();
        }, new GameEvent { TournamentPlace = place });
        return r with { StartedNewRun = reset };
    }

    /// <summary>Counts a tournament hand for the statistics (no chips move).</summary>
    public static OperationResult Hand(PlayerState s, HandOutcomeDto hand) =>
        Run(s, (b, _) => b.RecordHand(hand.Won, hand.Pot), hand.ToEvent());

    public static OperationResult Exchange(PlayerState s, int chips)
    {
        if (chips <= 0 || chips % Bankroll.ExchangeRate != 0 || chips > s.Bankroll.ExchangeableChips)
            return OperationResult.Fail("Error.Request");
        return Run(s, (b, _) => b.Exchange(chips));
    }

    public static OperationResult Buy(PlayerState s, string itemId)
    {
        if (ShopCatalog.Find(itemId) is not { } item || s.Collection.Owns(item)) return OperationResult.Fail("Error.Request");
        if (s.Bankroll.Crowns < item.Price) return OperationResult.Fail("Lobby.NotEnough");
        return Run(s, (b, c) => c.Buy(item, b));
    }

    public static OperationResult Equip(PlayerState s, string itemId)
    {
        if (ShopCatalog.Find(itemId) is not { } item || !s.Collection.Owns(item)) return OperationResult.Fail("Error.Request");
        return Run(s, (_, c) => c.Equip(item));
    }

    public static OperationResult LessonsCompleted(PlayerState s) =>
        Run(s, (_, _) => { }, new GameEvent { LessonsCompleted = true });

    /// <summary>Checks the format only; the server also checks that nobody else has the name.</summary>
    public static OperationResult Rename(PlayerState s, string name)
    {
        if (NameRules.Check(name) is { } error) return OperationResult.Fail(error);
        s.DisplayName = name.Trim();
        return new OperationResult([]);
    }

    /// <summary>Applies a change and checks achievements before and after (pays their crowns).</summary>
    private static OperationResult Run(PlayerState s, Action<Bankroll, PlayerCollection> change, GameEvent? e = null) =>
        Run(s, change, () => e ?? GameEvent.None);

    /// <param name="eventAfter">Built after the change (when it depends on the new values).</param>
    private static OperationResult Run(PlayerState s, Action<Bankroll, PlayerCollection> change, Func<GameEvent> eventAfter)
    {
        var before = Achievements.Check(s.Collection, s.Bankroll, GameEvent.None);
        change(s.Bankroll, s.Collection);
        var after = Achievements.Check(s.Collection, s.Bankroll, eventAfter());
        return new OperationResult([.. before.Concat(after).Select(a => a.Id)]);
    }
}
