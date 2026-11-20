using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Engine.Holdem;
using static ChipRoyale.Web.Localization.Loc;

namespace ChipRoyale.Web.Components.Game;

/// <summary>Localized texts for cards, hands and actions.</summary>
public static class HandText
{
    public static string One(Rank r) => L($"Rank.{r}");
    public static string Many(Rank r) => L($"Rank.{r}.Plural");

    public static string RankSymbol(Rank r) => r switch
    {
        Rank.Ten => "10",
        >= Rank.Jack => L($"Rank.{r}.Symbol"),
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

    public static string Category(HandCategory c) => L($"Hand.{c}");

    public static string Describe(HandValue v)
    {
        var k = v.Kickers;
        return v.Category switch
        {
            HandCategory.HighCard => L("Hand.Describe.HighCard", One(k[0])),
            HandCategory.OnePair => L("Hand.Describe.OnePair", Many(k[0])),
            HandCategory.TwoPair => L("Hand.Describe.TwoPair", Many(k[0]), Many(k[1])),
            HandCategory.ThreeOfAKind => L("Hand.Describe.ThreeOfAKind", Many(k[0])),
            HandCategory.Straight => L("Hand.Describe.Straight", One(k[0])),
            HandCategory.Flush => L("Hand.Describe.Flush", One(k[0])),
            HandCategory.FullHouse => L("Hand.Describe.FullHouse", Many(k[0]), Many(k[1])),
            HandCategory.FourOfAKind => L("Hand.Describe.FourOfAKind", Many(k[0])),
            _ when k[0] == Rank.Ace => L("Hand.RoyalFlush"),
            _ => L("Hand.Describe.StraightFlush", One(k[0])),
        };
    }

    /// <summary>Current made hand: 2 hole cards (preflop) or best of hole + board.</summary>
    public static string DescribeCurrent(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
    {
        if (hole.Count < 2) return "";
        if (board.Count >= 3)
            return Describe(HandEvaluator.EvaluateBest([.. hole, .. board]));

        var (a, b) = hole[0].Rank >= hole[1].Rank ? (hole[0], hole[1]) : (hole[1], hole[0]);
        if (a.Rank == b.Rank) return L("Hand.Describe.OnePair", Many(a.Rank));
        return L(a.Suit == b.Suit ? "Hand.Preflop.Suited" : "Hand.Preflop.Offsuit", One(a.Rank), One(b.Rank));
    }

    public static string Action(PlayerAction a) => a.Type switch
    {
        ActionType.Fold => L("Action.Fold"),
        ActionType.Check => L("Action.Check"),
        ActionType.Call => L("Action.Call", a.Amount),
        ActionType.Raise => L("Action.Raise", a.Amount),
        _ => L("Action.AllIn"),
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
