using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Tournament;

namespace ChipRoyale.Shared;

/// <summary>A player's game data while an operation runs on it.</summary>
public sealed class PlayerState(string displayName, Bankroll bankroll, PlayerCollection collection)
{
    public string DisplayName { get; set; } = displayName;
    public Bankroll Bankroll { get; } = bankroll;
    public PlayerCollection Collection { get; } = collection;

    public ProfileDto ToDto() => ProfileDto.From(DisplayName, Bankroll, Collection);

    public static PlayerState New(string displayName) => new(displayName, Bankroll.New(), new PlayerCollection());

    public static PlayerState FromDto(ProfileDto dto) =>
        new(dto.DisplayName, Bankroll.FromState(dto.Bankroll), dto.ToCollection());
}

/// <param name="Unlocked">Achievement ids unlocked by the operation.</param>
/// <param name="Error">Resource key of an error (the operation did nothing).</param>
public sealed record OperationResult(string[] Unlocked, bool StartedNewRun = false, int ReturnedFromTable = 0, string? Error = null)
{
    public ProfileResponse ToResponse(PlayerState state) => new(state.ToDto(), Unlocked, StartedNewRun, ReturnedFromTable, Error);

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
        if (TableStakes.Find(stakeId) is not { } stake) return OperationResult.Fail("Error.Request");
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
    private static OperationResult Run(PlayerState s, Action<Bankroll, PlayerCollection> change, GameEvent? e = null)
    {
        var before = Achievements.Check(s.Collection, s.Bankroll, GameEvent.None);
        change(s.Bankroll, s.Collection);
        var after = Achievements.Check(s.Collection, s.Bankroll, e ?? GameEvent.None);
        return new OperationResult([.. before.Concat(after).Select(a => a.Id)]);
    }
}
