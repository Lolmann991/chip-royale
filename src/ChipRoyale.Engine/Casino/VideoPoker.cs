using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Casino;

public enum VpHand
{
    Nothing,
    /// <summary>A pair of jacks, queens, kings or aces.</summary>
    JacksOrBetter,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush,
}

/// <summary>Why the hint holds these cards (resource key suffix).</summary>
public enum VpReason
{
    MadeHand,
    FourToRoyal,
    FourToStraightFlush,
    TwoPair,
    HighPair,
    ThreeToRoyal,
    FourToFlush,
    LowPair,
    FourToOutsideStraight,
    TwoSuitedHigh,
    ThreeToStraightFlush,
    TwoHigh,
    SuitedTenHigh,
    OneHigh,
    Nothing,
}

public sealed record VpAdvice(bool[] Hold, VpReason Reason);

/// <summary>Jacks or Better with the full-pay 9/6 table (99.5 % return with best play).</summary>
public static class JacksOrBetter
{
    /// <summary>Paid for one chip bet (the bet is included: a high pair returns the bet).</summary>
    public static readonly IReadOnlyDictionary<VpHand, int> PayTable = new Dictionary<VpHand, int>
    {
        [VpHand.RoyalFlush] = 800,
        [VpHand.StraightFlush] = 50,
        [VpHand.FourOfAKind] = 25,
        [VpHand.FullHouse] = 9,
        [VpHand.Flush] = 6,
        [VpHand.Straight] = 4,
        [VpHand.ThreeOfAKind] = 3,
        [VpHand.TwoPair] = 2,
        [VpHand.JacksOrBetter] = 1,
    };

    public static int Pays(VpHand hand) => PayTable.GetValueOrDefault(hand);

    public static VpHand Evaluate(IReadOnlyList<Card> five)
    {
        var v = HandEvaluator.EvaluateFive(five);
        return v.Category switch
        {
            HandCategory.StraightFlush when five.Min(c => c.Rank) == Rank.Ten => VpHand.RoyalFlush,
            HandCategory.StraightFlush => VpHand.StraightFlush,
            HandCategory.FourOfAKind => VpHand.FourOfAKind,
            HandCategory.FullHouse => VpHand.FullHouse,
            HandCategory.Flush => VpHand.Flush,
            HandCategory.Straight => VpHand.Straight,
            HandCategory.ThreeOfAKind => VpHand.ThreeOfAKind,
            HandCategory.TwoPair => VpHand.TwoPair,
            HandCategory.OnePair when v.Kickers[0] >= Rank.Jack => VpHand.JacksOrBetter,
            _ => VpHand.Nothing,
        };
    }

    // ------------------------------------------------------------------ hold hints (simple strategy)

    private static bool High(Card c) => c.Rank >= Rank.Jack;

    public static VpAdvice Advise(IReadOnlyList<Card> five)
    {
        var hand = Evaluate(five);
        var all = Enumerable.Range(0, 5).ToArray();

        if (hand is VpHand.RoyalFlush or VpHand.StraightFlush or VpHand.FourOfAKind)
            return Hold(five, all, VpReason.MadeHand);
        if (ToRoyal(five, 4) is { } r4) return Hold(five, r4, VpReason.FourToRoyal);
        if (hand is VpHand.FullHouse or VpHand.Flush or VpHand.ThreeOfAKind or VpHand.Straight)
            return Hold(five, hand == VpHand.ThreeOfAKind ? OfAKind(five, 3) : all, VpReason.MadeHand);
        if (ToStraightFlush(five, 4) is { } sf4) return Hold(five, sf4, VpReason.FourToStraightFlush);
        if (hand == VpHand.TwoPair) return Hold(five, Pairs(five).SelectMany(p => p).ToArray(), VpReason.TwoPair);
        if (hand == VpHand.JacksOrBetter) return Hold(five, Pairs(five)[0], VpReason.HighPair);
        if (ToRoyal(five, 3) is { } r3) return Hold(five, r3, VpReason.ThreeToRoyal);
        if (Suited(five, 4) is { } f4) return Hold(five, f4, VpReason.FourToFlush);
        if (Pairs(five) is [var low]) return Hold(five, low, VpReason.LowPair);
        if (OutsideStraight(five) is { } os) return Hold(five, os, VpReason.FourToOutsideStraight);

        var highs = all.Where(i => High(five[i])).ToArray();
        var suitedHigh = highs.GroupBy(i => five[i].Suit).FirstOrDefault(g => g.Count() >= 2);
        if (suitedHigh is not null) return Hold(five, [.. suitedHigh.OrderBy(i => five[i].Rank).Take(2)], VpReason.TwoSuitedHigh);
        if (ToStraightFlush(five, 3) is { } sf3) return Hold(five, sf3, VpReason.ThreeToStraightFlush);
        if (highs.Length >= 2) return Hold(five, [.. highs.OrderBy(i => five[i].Rank).Take(2)], VpReason.TwoHigh);

        var ten = all.FirstOrDefault(i => five[i].Rank == Rank.Ten, -1);
        if (ten >= 0)
        {
            var partner = highs.FirstOrDefault(i => five[i].Suit == five[ten].Suit && five[i].Rank != Rank.Ace, -1);
            if (partner >= 0) return Hold(five, [ten, partner], VpReason.SuitedTenHigh);
        }
        if (highs.Length == 1) return Hold(five, highs, VpReason.OneHigh);
        return Hold(five, [], VpReason.Nothing);
    }

    private static VpAdvice Hold(IReadOnlyList<Card> five, int[] indices, VpReason reason)
    {
        var hold = new bool[five.Count];
        foreach (var i in indices) hold[i] = true;
        return new VpAdvice(hold, reason);
    }

    private static int[] OfAKind(IReadOnlyList<Card> five, int n) =>
        [.. Enumerable.Range(0, 5).GroupBy(i => five[i].Rank).First(g => g.Count() == n)];

    /// <summary>Pairs, highest first.</summary>
    private static int[][] Pairs(IReadOnlyList<Card> five) =>
        [.. Enumerable.Range(0, 5).GroupBy(i => five[i].Rank).Where(g => g.Count() == 2).OrderByDescending(g => g.Key).Select(g => g.ToArray())];

    private static int[]? Suited(IReadOnlyList<Card> five, int n) =>
        Enumerable.Range(0, 5).GroupBy(i => five[i].Suit).FirstOrDefault(g => g.Count() >= n)?.Take(n).ToArray();

    /// <summary>n cards of the same suit from 10, J, Q, K, A.</summary>
    private static int[]? ToRoyal(IReadOnlyList<Card> five, int n) =>
        Enumerable.Range(0, 5).Where(i => five[i].Rank >= Rank.Ten).GroupBy(i => five[i].Suit)
            .FirstOrDefault(g => g.Count() >= n)?.Take(n).ToArray();

    /// <summary>n suited cards that fit into one straight (ace may be low).</summary>
    private static int[]? ToStraightFlush(IReadOnlyList<Card> five, int n)
    {
        foreach (var g in Enumerable.Range(0, 5).GroupBy(i => five[i].Suit).Where(g => g.Count() >= n))
        {
            var idx = g.ToArray();
            foreach (var combo in Combinations(idx, n))
                if (FitsStraight(combo.Select(i => five[i].Rank).ToArray())) return combo;
        }
        return null;
    }

    /// <summary>Four different ranks in a row, open at both ends (not A-2-3-4 or J-Q-K-A).</summary>
    private static int[]? OutsideStraight(IReadOnlyList<Card> five)
    {
        foreach (var combo in Combinations([0, 1, 2, 3, 4], 4))
        {
            var ranks = combo.Select(i => (int)five[i].Rank).OrderBy(r => r).ToArray();
            if (ranks.Distinct().Count() == 4 && ranks[3] - ranks[0] == 3 && ranks[0] >= 3 && ranks[3] <= 13) return combo;
        }
        return null;
    }

    private static bool FitsStraight(Rank[] ranks)
    {
        if (ranks.Distinct().Count() != ranks.Length) return false;
        var r = ranks.Select(x => (int)x).ToArray();
        if (r.Max() - r.Min() <= 4) return true;
        // ace low: A-2-3-4-5
        return r.Contains(14) && r.Select(x => x == 14 ? 1 : x).Max() - r.Select(x => x == 14 ? 1 : x).Min() <= 4;
    }

    private static IEnumerable<int[]> Combinations(int[] items, int k)
    {
        if (k == 0) { yield return []; yield break; }
        for (var i = 0; i <= items.Length - k; i++)
            foreach (var rest in Combinations(items[(i + 1)..], k - 1))
                yield return [items[i], .. rest];
    }
}

/// <summary>One hand: deal five, hold some, draw the rest once.</summary>
public sealed class VideoPokerHand
{
    private readonly Deck _deck;
    private readonly Card[] _cards = new Card[5];

    public VideoPokerHand(int bet, Random? random = null)
    {
        Bet = bet;
        _deck = new Deck(random);
        _deck.Shuffle();
        for (var i = 0; i < 5; i++) _cards[i] = _deck.Draw();
    }

    /// <summary>For tests: these five cards, then the rest of the deck shuffled.</summary>
    public VideoPokerHand(int bet, IReadOnlyList<Card> first, IEnumerable<Card> draws)
    {
        Bet = bet;
        _deck = new Deck(new Random(1));
        for (var i = 0; i < 5; i++) _cards[i] = first[i];
        _next = new Queue<Card>(draws);
    }

    private readonly Queue<Card>? _next;

    public int Bet { get; }
    public IReadOnlyList<Card> Cards => _cards;
    public bool[] Held { get; } = new bool[5];
    public bool Drawn { get; private set; }

    public VpHand Hand => JacksOrBetter.Evaluate(_cards);

    /// <summary>Chips paid back (bet included) once drawn.</summary>
    public long Payout => Drawn ? (long)Bet * JacksOrBetter.Pays(Hand) : 0;

    public void Toggle(int i)
    {
        if (!Drawn) Held[i] = !Held[i];
    }

    public void Draw()
    {
        if (Drawn) throw new InvalidOperationException("Already drawn.");
        var dealt = new HashSet<Card>(_cards);
        for (var i = 0; i < 5; i++)
        {
            if (Held[i]) continue;
            Card c;
            do c = _next is { Count: > 0 } ? _next.Dequeue() : _deck.Draw();
            while (dealt.Contains(c));
            dealt.Add(c);
            _cards[i] = c;
        }
        Drawn = true;
    }
}
