using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Holdem;

/// <summary>One main or side pot and who won it.</summary>
public sealed record PotResult(int Amount, IReadOnlyList<int> Winners, IReadOnlyList<int> Eligible);

public sealed class HandResult
{
    public HandResult(
        bool wasShowdown,
        IReadOnlyList<PotResult> pots,
        IReadOnlyDictionary<int, HandValue> hands,
        IReadOnlyDictionary<int, int> winnings)
    {
        WasShowdown = wasShowdown;
        Pots = pots;
        Hands = hands;
        Winnings = winnings;
    }

    /// <summary>False when everybody else folded.</summary>
    public bool WasShowdown { get; }

    public IReadOnlyList<PotResult> Pots { get; }

    /// <summary>Best hand of every player still in at showdown (empty without showdown).</summary>
    public IReadOnlyDictionary<int, HandValue> Hands { get; }

    /// <summary>Chips each winner received from the pot(s).</summary>
    public IReadOnlyDictionary<int, int> Winnings { get; }

    public int TotalPot => Pots.Sum(p => p.Amount);

    public IEnumerable<int> WinnerIndices => Winnings.Where(w => w.Value > 0).Select(w => w.Key);
}
