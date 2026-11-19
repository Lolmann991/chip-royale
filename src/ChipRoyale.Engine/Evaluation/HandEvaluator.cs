using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Evaluation;

public enum HandCategory
{
    HighCard,
    OnePair,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
}

/// <summary>
/// Strength of a 5-card poker hand. Compare with &lt;, &gt; or CompareTo;
/// equal values mean a split pot.
/// </summary>
public sealed class HandValue : IComparable<HandValue>
{
    public HandValue(HandCategory category, IReadOnlyList<Rank> kickers, IReadOnlyList<Card> cards)
    {
        Category = category;
        Kickers = kickers;
        Cards = cards;
    }

    public HandCategory Category { get; }

    /// <summary>Ranks in tie-break order (e.g. pair rank first, then kickers).</summary>
    public IReadOnlyList<Rank> Kickers { get; }

    /// <summary>The five cards forming this hand.</summary>
    public IReadOnlyList<Card> Cards { get; }

    public int CompareTo(HandValue? other)
    {
        if (other is null) return 1;
        var c = Category.CompareTo(other.Category);
        if (c != 0) return c;
        for (var i = 0; i < Math.Min(Kickers.Count, other.Kickers.Count); i++)
        {
            c = Kickers[i].CompareTo(other.Kickers[i]);
            if (c != 0) return c;
        }
        return 0;
    }

    public static bool operator >(HandValue a, HandValue b) => a.CompareTo(b) > 0;
    public static bool operator <(HandValue a, HandValue b) => a.CompareTo(b) < 0;
    public static bool operator >=(HandValue a, HandValue b) => a.CompareTo(b) >= 0;
    public static bool operator <=(HandValue a, HandValue b) => a.CompareTo(b) <= 0;

    public override string ToString() => $"{Category} [{string.Join(" ", Cards)}]";
}

public static class HandEvaluator
{
    /// <summary>Best 5-card hand from 5 to 7 cards (Hold'em, Five Card Draw).</summary>
    public static HandValue EvaluateBest(IReadOnlyList<Card> cards)
    {
        if (cards.Count < 5 || cards.Count > 7)
            throw new ArgumentException("Need between 5 and 7 cards.", nameof(cards));

        HandValue? best = null;
        foreach (var combo in Combinations(cards, 5))
        {
            var value = EvaluateFive(combo);
            if (best is null || value > best)
                best = value;
        }
        return best!;
    }

    /// <summary>Omaha rule: exactly 2 hole cards plus exactly 3 board cards.</summary>
    public static HandValue EvaluateOmaha(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> board)
    {
        if (holeCards.Count != 4)
            throw new ArgumentException("Omaha needs 4 hole cards.", nameof(holeCards));
        if (board.Count < 3 || board.Count > 5)
            throw new ArgumentException("Board needs 3 to 5 cards.", nameof(board));

        HandValue? best = null;
        foreach (var hole in Combinations(holeCards, 2))
            foreach (var common in Combinations(board, 3))
            {
                var value = EvaluateFive([.. hole, .. common]);
                if (best is null || value > best)
                    best = value;
            }
        return best!;
    }

    public static HandValue EvaluateFive(IReadOnlyList<Card> cards)
    {
        if (cards.Count != 5)
            throw new ArgumentException("Need exactly 5 cards.", nameof(cards));

        var sorted = cards.OrderByDescending(c => c.Rank).ToArray();
        var isFlush = sorted.All(c => c.Suit == sorted[0].Suit);
        var straightHigh = GetStraightHigh(sorted);

        // Groups ordered by size, then by rank: e.g. full house K K K 4 4 -> [K, 4]
        var groups = sorted
            .GroupBy(c => c.Rank)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToArray();
        var groupRanks = groups.Select(g => g.Key).ToArray();

        if (straightHigh is { } sh && isFlush)
            return new HandValue(HandCategory.StraightFlush, [sh], sorted);
        if (groups[0].Count() == 4)
            return new HandValue(HandCategory.FourOfAKind, groupRanks, sorted);
        if (groups[0].Count() == 3 && groups[1].Count() == 2)
            return new HandValue(HandCategory.FullHouse, groupRanks, sorted);
        if (isFlush)
            return new HandValue(HandCategory.Flush, sorted.Select(c => c.Rank).ToArray(), sorted);
        if (straightHigh is { } high)
            return new HandValue(HandCategory.Straight, [high], sorted);
        if (groups[0].Count() == 3)
            return new HandValue(HandCategory.ThreeOfAKind, groupRanks, sorted);
        if (groups[0].Count() == 2 && groups[1].Count() == 2)
            return new HandValue(HandCategory.TwoPair, groupRanks, sorted);
        if (groups[0].Count() == 2)
            return new HandValue(HandCategory.OnePair, groupRanks, sorted);

        return new HandValue(HandCategory.HighCard, groupRanks, sorted);
    }

    private static Rank? GetStraightHigh(Card[] sortedDesc)
    {
        var ranks = sortedDesc.Select(c => (int)c.Rank).Distinct().ToArray();
        if (ranks.Length != 5) return null;
        if (ranks[0] - ranks[4] == 4) return (Rank)ranks[0];
        // Wheel: A 5 4 3 2
        if (ranks.SequenceEqual([14, 5, 4, 3, 2])) return Rank.Five;
        return null;
    }

    private static IEnumerable<Card[]> Combinations(IReadOnlyList<Card> source, int k)
    {
        var indices = Enumerable.Range(0, k).ToArray();
        var n = source.Count;
        while (true)
        {
            yield return indices.Select(i => source[i]).ToArray();

            var pos = k - 1;
            while (pos >= 0 && indices[pos] == n - k + pos) pos--;
            if (pos < 0) yield break;

            indices[pos]++;
            for (var i = pos + 1; i < k; i++)
                indices[i] = indices[i - 1] + 1;
        }
    }
}
