namespace ChipRoyale.Engine.Holdem;

public enum ActionType
{
    Fold,
    Check,
    Call,
    /// <summary>Bet or raise. <see cref="PlayerAction.Amount"/> is the total to raise to on this street.</summary>
    Raise,
    AllIn,
}

/// <param name="Amount">
/// For <see cref="ActionType.Raise"/>: the street total to raise to. After the action is applied
/// the table records the effective amount (call size, raise-to total or all-in total).
/// </param>
public readonly record struct PlayerAction(ActionType Type, int Amount = 0)
{
    public static PlayerAction Fold() => new(ActionType.Fold);
    public static PlayerAction Check() => new(ActionType.Check);
    public static PlayerAction Call() => new(ActionType.Call);
    public static PlayerAction RaiseTo(int total) => new(ActionType.Raise, total);
    public static PlayerAction AllIn() => new(ActionType.AllIn);
}

public sealed record LegalActions(
    bool CanCheck,
    int ToCall,
    bool CanRaise,
    int MinRaiseTo,
    int MaxRaiseTo,
    int CurrentBet,
    int StreetBet,
    int Pot);

public enum Street
{
    Preflop,
    Flop,
    Turn,
    River,
    Showdown,
}

public enum TablePhase
{
    /// <summary>No hand started yet.</summary>
    Idle,
    /// <summary><see cref="HoldemTable.ActingIndex"/> has to act.</summary>
    WaitingForAction,
    /// <summary>A betting round is over; call <see cref="HoldemTable.Advance"/> to deal the next street or show down.</summary>
    RoundComplete,
    HandComplete,
}

public sealed record ActionLogEntry(int HandNumber, Street Street, int PlayerIndex, PlayerAction Action);
