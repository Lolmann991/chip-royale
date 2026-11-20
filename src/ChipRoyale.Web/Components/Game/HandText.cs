using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Engine.Holdem;

namespace ChipRoyale.Web.Components.Game;

/// <summary>German texts for cards, hands and actions.</summary>
public static class HandText
{
    private static readonly Dictionary<Rank, (string One, string Many)> Names = new()
    {
        [Rank.Two] = ("Zwei", "Zweien"),
        [Rank.Three] = ("Drei", "Dreien"),
        [Rank.Four] = ("Vier", "Vieren"),
        [Rank.Five] = ("Fünf", "Fünfen"),
        [Rank.Six] = ("Sechs", "Sechsen"),
        [Rank.Seven] = ("Sieben", "Siebenen"),
        [Rank.Eight] = ("Acht", "Achten"),
        [Rank.Nine] = ("Neun", "Neunen"),
        [Rank.Ten] = ("Zehn", "Zehnen"),
        [Rank.Jack] = ("Bube", "Buben"),
        [Rank.Queen] = ("Dame", "Damen"),
        [Rank.King] = ("König", "Könige"),
        [Rank.Ace] = ("Ass", "Asse"),
    };

    public static string One(Rank r) => Names[r].One;
    public static string Many(Rank r) => Names[r].Many;

    public static string RankSymbol(Rank r) => r switch
    {
        Rank.Ten => "10",
        Rank.Jack => "B",
        Rank.Queen => "D",
        Rank.King => "K",
        Rank.Ace => "A",
        _ => ((int)r).ToString(),
    };

    public static string SuitSymbol(Suit s) => s switch
    {
        Suit.Clubs => "♣",
        Suit.Diamonds => "♦",
        Suit.Hearts => "♥",
        _ => "♠",
    };

    public static bool IsRed(Suit s) => s is Suit.Hearts or Suit.Diamonds;

    public static string Category(HandCategory c) => c switch
    {
        HandCategory.HighCard => "Höchste Karte",
        HandCategory.OnePair => "Paar",
        HandCategory.TwoPair => "Zwei Paare",
        HandCategory.ThreeOfAKind => "Drilling",
        HandCategory.Straight => "Straße",
        HandCategory.Flush => "Flush",
        HandCategory.FullHouse => "Full House",
        HandCategory.FourOfAKind => "Vierling",
        _ => "Straight Flush",
    };

    public static string Describe(HandValue v)
    {
        var k = v.Kickers;
        return v.Category switch
        {
            HandCategory.HighCard => $"{One(k[0])} hoch",
            HandCategory.OnePair => $"Paar {Many(k[0])}",
            HandCategory.TwoPair => $"Zwei Paare, {Many(k[0])} und {Many(k[1])}",
            HandCategory.ThreeOfAKind => $"Drilling {Many(k[0])}",
            HandCategory.Straight => $"Straße bis {One(k[0])}",
            HandCategory.Flush => $"Flush, {One(k[0])} hoch",
            HandCategory.FullHouse => $"Full House, {Many(k[0])} über {Many(k[1])}",
            HandCategory.FourOfAKind => $"Vierling {Many(k[0])}",
            _ when k[0] == Rank.Ace => "Royal Flush",
            _ => $"Straight Flush bis {One(k[0])}",
        };
    }

    /// <summary>Current made hand: 2 hole cards (preflop) or best of hole + board.</summary>
    public static string DescribeCurrent(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
    {
        if (hole.Count < 2) return "";
        if (board.Count >= 3)
            return Describe(HandEvaluator.EvaluateBest([.. hole, .. board]));

        var (a, b) = hole[0].Rank >= hole[1].Rank ? (hole[0], hole[1]) : (hole[1], hole[0]);
        if (a.Rank == b.Rank) return $"Paar {Many(a.Rank)}";
        var suited = a.Suit == b.Suit ? ", gleiche Farbe" : "";
        return $"{One(a.Rank)}-{One(b.Rank)}{suited}";
    }

    public static string Action(PlayerAction a) => a.Type switch
    {
        ActionType.Fold => "Passt",
        ActionType.Check => "Checkt",
        ActionType.Call => $"Geht mit {a.Amount:N0}",
        ActionType.Raise => $"Erhöht auf {a.Amount:N0}",
        _ => "All-in!",
    };

    public static string Street(Street s) => s switch
    {
        Engine.Holdem.Street.Preflop => "Preflop",
        Engine.Holdem.Street.Flop => "Flop",
        Engine.Holdem.Street.Turn => "Turn",
        Engine.Holdem.Street.River => "River",
        _ => "Showdown",
    };
}
