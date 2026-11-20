using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Holdem;

public sealed class Player
{
    internal readonly List<Card> Hole = new(2);

    public Player(string name, int chips, bool isHuman = false)
    {
        Name = name;
        Chips = chips;
        IsHuman = isHuman;
    }

    public string Name { get; }
    public bool IsHuman { get; }

    /// <summary>Chips behind (not yet put into the pot).</summary>
    public int Chips { get; internal set; }

    public IReadOnlyList<Card> HoleCards => Hole;

    /// <summary>Chips put in during the current betting round.</summary>
    public int StreetBet { get; internal set; }

    /// <summary>Chips put in during the whole hand (including <see cref="StreetBet"/>).</summary>
    public int TotalInvested { get; internal set; }

    /// <summary>Was dealt into the current (or last) hand.</summary>
    public bool InHand { get; internal set; }

    public bool HasFolded { get; internal set; }

    public PlayerAction? LastAction { get; internal set; }

    internal bool HasActed { get; set; }

    /// <summary>Still contesting the pot.</summary>
    public bool IsActive => InHand && !HasFolded;

    public bool IsAllIn => IsActive && Chips == 0;

    /// <summary>Can still make betting decisions this hand.</summary>
    public bool CanAct => IsActive && Chips > 0;

    /// <summary>Lost all chips; no longer dealt in.</summary>
    public bool IsEliminated { get; internal set; }

    public override string ToString() => $"{Name} ({Chips})";
}
