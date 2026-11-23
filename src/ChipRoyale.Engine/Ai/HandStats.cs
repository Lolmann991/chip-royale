using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Ai;

/// <summary>Detailed odds for one player's hand (for the live tips).</summary>
public sealed record HandOdds(
    double Win,
    double Tie,
    double Lose,
    double Equity,
    IReadOnlyDictionary<HandCategory, double> FinalCategory);

/// <summary>Cards that improve the current made hand, and the chance to hit one by the river.</summary>
public sealed record Outs(int Count, HandCategory Current, HandCategory Best, double HitChance);

public static class HandStats
{
    /// <summary>
    /// Monte-Carlo odds against random hands: win / split / lose, equity, and how likely each
    /// hand category is for the player once all five board cards are out.
    /// </summary>
    public static HandOdds Analyze(IReadOnlyList<Card> hole, IReadOnlyList<Card> board, int opponents,
        int iterations = 3000, Random? random = null)
    {
        if (hole.Count != 2) throw new ArgumentException("Need two hole cards.", nameof(hole));
        opponents = Math.Clamp(opponents, 1, 9);
        random ??= Random.Shared;

        var deck = Unseen(hole, board);
        var missing = 5 - board.Count;
        var needed = missing + opponents * 2;
        Span<Card> mine = stackalloc Card[7];
        Span<Card> theirs = stackalloc Card[7];
        var categories = new int[Enum.GetValues<HandCategory>().Length];
        int wins = 0, ties = 0;
        double equity = 0;

        for (var it = 0; it < iterations; it++)
        {
            for (var i = 0; i < needed; i++)
            {
                var j = i + random.Next(deck.Length - i);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            mine[0] = hole[0];
            mine[1] = hole[1];
            for (var i = 0; i < board.Count; i++) mine[2 + i] = board[i];
            for (var i = 0; i < missing; i++) mine[2 + board.Count + i] = deck[i];
            var my = FastEvaluator.Score(mine);
            categories[(int)FastEvaluator.CategoryOf(my)]++;

            mine[2..].CopyTo(theirs[2..]);
            var beaten = false;
            var tied = 1;
            for (var o = 0; o < opponents && !beaten; o++)
            {
                theirs[0] = deck[missing + o * 2];
                theirs[1] = deck[missing + o * 2 + 1];
                var score = FastEvaluator.Score(theirs);
                if (score > my) beaten = true;
                else if (score == my) tied++;
            }
            if (beaten) continue;
            if (tied == 1) wins++; else ties++;
            equity += 1.0 / tied;
        }

        var dist = Enum.GetValues<HandCategory>().ToDictionary(c => c, c => categories[(int)c] / (double)iterations);
        return new HandOdds(
            Win: wins / (double)iterations,
            Tie: ties / (double)iterations,
            Lose: 1 - (wins + ties) / (double)iterations,
            Equity: equity / iterations,
            FinalCategory: dist);
    }

    /// <summary>
    /// Outs on the flop or turn: unseen cards that give a better hand category than now
    /// (and better than what the board alone would show).
    /// Hit chance is for the next card on the turn, and for turn or river on the flop.
    /// </summary>
    public static Outs? CountOuts(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
    {
        if (hole.Count != 2 || board.Count is < 3 or > 4) return null;
        var current = HandEvaluator.EvaluateBest([.. hole, .. board]).Category;
        var unseen = Unseen(hole, board);
        var count = 0;
        var best = current;
        foreach (var card in unseen)
        {
            var category = HandEvaluator.EvaluateBest([.. hole, .. board, card]).Category;
            if (category <= current) continue;
            // Not an out if the board alone gets just as good (e.g. the board pairs).
            var boardOnly = FastEvaluator.CategoryOf(FastEvaluator.Score([.. board, card]));
            if (category <= boardOnly) continue;
            count++;
            if (category > best) best = category;
        }

        double n = unseen.Length;
        var chance = board.Count == 4
            ? count / n
            : 1 - (n - count) / n * ((n - 1 - count) / (n - 1));
        return new Outs(count, current, best, chance);
    }

    private static Card[] Unseen(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
    {
        var known = new HashSet<Card>(hole.Concat(board));
        var list = new List<Card>(52);
        foreach (var suit in Enum.GetValues<Suit>())
            foreach (var rank in Enum.GetValues<Rank>())
            {
                var c = new Card(rank, suit);
                if (!known.Contains(c)) list.Add(c);
            }
        return list.ToArray();
    }
}

/// <summary>
/// Ranking of the 169 distinct starting hands (pairs, suited, offsuit) by their chance to win
/// heads-up against a random hand. The order was computed once with 30,000 samples per hand
/// and is stored here, so nothing has to be simulated at runtime (important in the browser).
/// </summary>
public static class StartingHands
{
    public const int Count = 169;

    private const string Order =
        "AA KK QQ JJ TT 99 88 AKs AQs 77 AKo AJs ATs AQo AJo KQs 66 A9s KJs ATo A8s KTs KQo A7s 55 A9o KJo QJs A5s A6s K9s A8o KTo QTs A4s A7o QJo K8s A3s A6o JTs Q9s K7s A2s K9o A5o QTo A4o 44 K6s K8o Q8s J9s A3o K5s Q9o JTo K7o K4s A2o J8s T9s Q7s K6o K3s Q8o 33 K5o K2s Q6s J9o K4o J7s Q5s Q7o T8s Q4s Q6o K3o T9o J8o Q3s 98s T7s J6s J5s T8o 22 Q5o K2o Q2s J4s J7o Q4o 97s T6s 98o Q3o J3s J6o T7o Q2o 87s J5o 96s J2s T4s T5s 86s 97o J4o 76s 95s T3s T6o J3o T2s 87o J2o T5o 85s 96o 75s 94s T4o 93s T3o 92s 86o 65s 84s 76o 95o 85o T2o 74s 83s 54s 64s 65o 75o 94o 82s 53s 93o 73s 63s 84o 92o 74o 54o 43s 64o 72s 52s 42s 62s 83o 73o 82o 63o 53o 32s 72o 43o 52o 62o 42o 32o";

    private static readonly Dictionary<string, int> Ranks = Order
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select((key, i) => (key, rank: i + 1))
        .ToDictionary(x => x.key, x => x.rank);

    /// <summary>Key like "AKs", "QJo" or "77".</summary>
    public static string Key(Card a, Card b)
    {
        var (hi, lo) = a.Rank >= b.Rank ? (a, b) : (b, a);
        var r = $"{RankChar(hi.Rank)}{RankChar(lo.Rank)}";
        if (hi.Rank == lo.Rank) return r;
        return r + (hi.Suit == lo.Suit ? "s" : "o");
    }

    /// <summary>1 = best (AA) … 169 = worst.</summary>
    public static int RankOf(Card a, Card b) => Ranks[Key(a, b)];

    /// <summary>Share of starting hands that are at least as good, e.g. 0.05 = top 5 %.</summary>
    public static double TopPercent(Card a, Card b)
    {
        var rank = RankOf(a, b);
        // Weight by how many card combinations each class has (pairs 6, suited 4, offsuit 12).
        var combos = 0;
        foreach (var (key, r) in Ranks)
            if (r <= rank) combos += key.Length == 2 ? 6 : key[2] == 's' ? 4 : 12;
        return combos / 1326.0;
    }

    private static char RankChar(Rank r) => "23456789TJQKA"[(int)r - 2];
}
