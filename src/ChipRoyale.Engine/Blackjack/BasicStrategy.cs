using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Blackjack;

/// <summary>Why a move is suggested (for the tip text).</summary>
public enum BjHandKind
{
    Hard,
    Soft,
    Pair,
}

public sealed record BjAdvice(BjAction Action, BjHandKind Kind, int Total);

/// <summary>
/// Basic strategy for 6 decks, dealer stands on soft 17, double after split, no surrender –
/// the mathematically best move without counting cards.
/// </summary>
public static class BasicStrategy
{
    public static BjAdvice Suggest(BjHand hand, Card dealerUp, bool canDouble, bool canSplit)
    {
        var up = BjValue.Of(dealerUp.Rank); // 2..11
        var (total, soft) = BjValue.Of(hand.Cards);

        if (canSplit && hand.Cards.Count == 2 && BjValue.Of(hand.Cards[0].Rank) == BjValue.Of(hand.Cards[1].Rank))
        {
            var pair = BjValue.Of(hand.Cards[0].Rank);
            var split = pair switch
            {
                11 or 8 => true,
                10 or 5 => false,
                9 => up is >= 2 and <= 9 && up != 7,
                7 => up <= 7,
                6 => up <= 6,
                4 => up is 5 or 6,
                3 or 2 => up <= 7,
                _ => false,
            };
            if (split) return new BjAdvice(BjAction.Split, BjHandKind.Pair, total);
        }

        if (soft)
        {
            var action = total switch
            {
                >= 19 => BjAction.Stand,
                18 => up is >= 3 and <= 6 ? (canDouble ? BjAction.Double : BjAction.Stand) : up is 2 or 7 or 8 ? BjAction.Stand : BjAction.Hit,
                17 => up is >= 3 and <= 6 && canDouble ? BjAction.Double : BjAction.Hit,
                16 or 15 => up is >= 4 and <= 6 && canDouble ? BjAction.Double : BjAction.Hit,
                _ => up is 5 or 6 && canDouble ? BjAction.Double : BjAction.Hit,
            };
            return new BjAdvice(action, BjHandKind.Soft, total);
        }

        var hard = total switch
        {
            >= 17 => BjAction.Stand,
            >= 13 => up <= 6 ? BjAction.Stand : BjAction.Hit,
            12 => up is >= 4 and <= 6 ? BjAction.Stand : BjAction.Hit,
            11 => up <= 10 && canDouble ? BjAction.Double : BjAction.Hit,
            10 => up <= 9 && canDouble ? BjAction.Double : BjAction.Hit,
            9 => up is >= 3 and <= 6 && canDouble ? BjAction.Double : BjAction.Hit,
            _ => BjAction.Hit,
        };
        return new BjAdvice(hard, BjHandKind.Hard, total);
    }

    /// <summary>
    /// Chance to go over 21 with one more card, from the cards still in the shoe
    /// (plus the dealer's hidden card, which the player cannot see). Soft hands cannot bust.
    /// </summary>
    public static double BustChance(BjHand hand, int[] remainingByValue, Card? hidden = null)
    {
        var (total, soft) = BjValue.Of(hand.Cards);
        if (soft || total <= 11) return 0;
        var counts = (int[])remainingByValue.Clone();
        if (hidden is { } h) counts[BjValue.Of(h.Rank)]++;
        var all = counts.Sum();
        if (all == 0) return 0;
        var busting = 0;
        for (var v = 2; v <= 10; v++)
            if (total + v > 21) busting += counts[v];
        return busting / (double)all; // an ace counts 1 and never busts
    }

    /// <summary>How often the dealer busts, by up card (6 decks, stands on soft 17, after the peek).</summary>
    public static double DealerBustChance(Card up) => BjValue.Of(up.Rank) switch
    {
        2 => 0.354,
        3 => 0.375,
        4 => 0.401,
        5 => 0.429,
        6 => 0.422,
        7 => 0.262,
        8 => 0.244,
        9 => 0.229,
        10 => 0.214,
        _ => 0.117,
    };
}
