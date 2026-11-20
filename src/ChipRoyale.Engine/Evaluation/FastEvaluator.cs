using System.Numerics;
using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Evaluation;

/// <summary>
/// Allocation-free hand scorer for 5 to 7 cards. Returns an integer where a higher
/// value is a stronger hand and equal values are a split. Orders hands exactly like
/// <see cref="HandEvaluator.EvaluateBest"/>, but is much faster, which matters for
/// Monte-Carlo equity estimates (thousands of evaluations per decision).
/// </summary>
public static class FastEvaluator
{
    public static int Score(ReadOnlySpan<Card> cards)
    {
        Span<int> rankCount = stackalloc int[15];
        Span<int> suitMask = stackalloc int[4];
        var rankMask = 0;

        foreach (var c in cards)
        {
            var r = (int)c.Rank;
            rankCount[r]++;
            rankMask |= 1 << r;
            suitMask[(int)c.Suit] |= 1 << r;
        }

        // Flush / straight flush
        for (var s = 0; s < 4; s++)
        {
            if (BitOperations.PopCount((uint)suitMask[s]) < 5) continue;
            var sf = StraightHigh(suitMask[s]);
            if (sf > 0) return Pack(HandCategory.StraightFlush, sf);
            return Pack(HandCategory.Flush, TopRanks(suitMask[s], 5));
        }

        int quad = 0, trip1 = 0, trip2 = 0, pair1 = 0, pair2 = 0, pair3 = 0;
        for (var r = 14; r >= 2; r--)
        {
            switch (rankCount[r])
            {
                case 4: quad = r; break;
                case 3:
                    if (trip1 == 0) trip1 = r; else if (trip2 == 0) trip2 = r;
                    break;
                case 2:
                    if (pair1 == 0) pair1 = r; else if (pair2 == 0) pair2 = r; else if (pair3 == 0) pair3 = r;
                    break;
            }
        }

        if (quad > 0)
            return Pack(HandCategory.FourOfAKind, quad, HighestExcept(rankMask, quad));

        if (trip1 > 0 && (trip2 > 0 || pair1 > 0))
            return Pack(HandCategory.FullHouse, trip1, Math.Max(trip2, pair1));

        var straight = StraightHigh(rankMask);
        if (straight > 0)
            return Pack(HandCategory.Straight, straight);

        if (trip1 > 0)
        {
            var rest = TopRanks(rankMask & ~(1 << trip1), 2);
            return Pack(HandCategory.ThreeOfAKind, trip1, rest);
        }

        if (pair1 > 0 && pair2 > 0)
        {
            var kicker = HighestExcept(rankMask, pair1, pair2);
            return Pack(HandCategory.TwoPair, pair1, pair2, kicker);
        }

        if (pair1 > 0)
            return Pack(HandCategory.OnePair, pair1, TopRanks(rankMask & ~(1 << pair1), 3));

        return Pack(HandCategory.HighCard, TopRanks(rankMask, 5));
    }

    public static HandCategory CategoryOf(int score) => (HandCategory)(score >> 20);

    private static int StraightHigh(int mask)
    {
        // Ace also counts as 1 for the wheel.
        if ((mask & (1 << 14)) != 0) mask |= 1 << 1;
        for (var high = 14; high >= 5; high--)
        {
            var run = 0b11111 << (high - 4);
            if ((mask & run) == run) return high;
        }
        return 0;
    }

    private static int HighestExcept(int mask, int a, int b = 0)
    {
        mask &= ~(1 << a);
        if (b > 0) mask &= ~(1 << b);
        return mask == 0 ? 0 : 31 - BitOperations.LeadingZeroCount((uint)mask);
    }

    private static int[] TopRanks(int mask, int count)
    {
        var result = new int[count];
        var i = 0;
        for (var r = 14; r >= 2 && i < count; r--)
            if ((mask & (1 << r)) != 0) result[i++] = r;
        return result;
    }

    private static int Pack(HandCategory category, params int[] kickers) => Pack(category, 0, kickers);

    private static int Pack(HandCategory category, int first, int[] rest)
    {
        var score = (int)category << 20;
        var shift = 16;
        if (first > 0)
        {
            score |= first << shift;
            shift -= 4;
        }
        foreach (var k in rest)
        {
            if (shift < 0) break;
            score |= k << shift;
            shift -= 4;
        }
        return score;
    }

    private static int Pack(HandCategory category, int a, int b) => Pack(category, a, [b]);
    private static int Pack(HandCategory category, int a, int b, int c) => Pack(category, a, [b, c]);
}
