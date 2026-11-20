using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Ai;

/// <summary>Monte-Carlo estimate of the chance to win against random opponent hands.</summary>
public static class EquityCalculator
{
    /// <returns>Expected pot share in [0, 1]; split pots count fractionally.</returns>
    public static double Estimate(
        IReadOnlyList<Card> hole,
        IReadOnlyList<Card> board,
        int opponents,
        int iterations = 400,
        Random? random = null)
    {
        if (hole.Count != 2) throw new ArgumentException("Need two hole cards.", nameof(hole));
        if (board.Count > 5) throw new ArgumentException("Board has at most five cards.", nameof(board));
        opponents = Math.Clamp(opponents, 1, 9);
        random ??= Random.Shared;

        var known = new HashSet<Card>(hole.Concat(board));
        var stub = new List<Card>(52 - known.Count);
        foreach (var suit in Enum.GetValues<Suit>())
            foreach (var rank in Enum.GetValues<Rank>())
            {
                var c = new Card(rank, suit);
                if (!known.Contains(c)) stub.Add(c);
            }

        var deck = stub.ToArray();
        var missingBoard = 5 - board.Count;
        var needed = missingBoard + opponents * 2;
        Span<Card> mine = stackalloc Card[7];
        Span<Card> theirs = stackalloc Card[7];
        double total = 0;

        for (var it = 0; it < iterations; it++)
        {
            // Partial Fisher-Yates: the first `needed` cards are a random draw.
            for (var i = 0; i < needed; i++)
            {
                var j = i + random.Next(deck.Length - i);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }

            for (var i = 0; i < board.Count; i++) mine[2 + i] = board[i];
            for (var i = 0; i < missingBoard; i++) mine[2 + board.Count + i] = deck[i];
            mine[0] = hole[0];
            mine[1] = hole[1];
            var myScore = FastEvaluator.Score(mine);

            var best = myScore;
            var tiedWithMe = 1;
            var beaten = false;
            mine[2..].CopyTo(theirs[2..]);
            for (var o = 0; o < opponents; o++)
            {
                theirs[0] = deck[missingBoard + o * 2];
                theirs[1] = deck[missingBoard + o * 2 + 1];
                var score = FastEvaluator.Score(theirs);
                if (score > best)
                {
                    beaten = true;
                    break;
                }
                if (score == myScore) tiedWithMe++;
            }

            if (!beaten) total += 1.0 / tiedWithMe;
        }

        return total / iterations;
    }
}
