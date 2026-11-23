using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Tests;

public class HandStatsTests
{
    [Fact]
    public void Odds_add_up_and_match_the_equity_calculator()
    {
        var odds = HandStats.Analyze(Card.ParseMany("Ah Kh"), Card.ParseMany("Qh 7c 2h"), 1, 4000, new Random(2));
        Assert.Equal(1.0, odds.Win + odds.Tie + odds.Lose, 6);
        Assert.Equal(1.0, odds.FinalCategory.Values.Sum(), 6);
        var reference = EquityCalculator.Estimate(Card.ParseMany("Ah Kh"), Card.ParseMany("Qh 7c 2h"), 1, 4000, new Random(3));
        Assert.InRange(odds.Equity, reference - 0.04, reference + 0.04);
        // Flush draw: the flush comes about a third of the time.
        Assert.InRange(odds.FinalCategory[HandCategory.Flush], 0.30, 0.40);
    }

    [Fact]
    public void Flush_draw_on_the_flop_has_nine_outs_plus_pairing_cards()
    {
        var outs = HandStats.CountOuts(Card.ParseMany("Ah Kh"), Card.ParseMany("Qh 7c 2h"))!;
        // 9 hearts for the flush + 3 aces + 3 kings for a pair.
        Assert.Equal(15, outs.Count);
        Assert.Equal(HandCategory.HighCard, outs.Current);
        Assert.Equal(HandCategory.Flush, outs.Best);
        Assert.InRange(outs.HitChance, 0.53, 0.56);
    }

    [Fact]
    public void Outs_only_on_flop_and_turn()
    {
        Assert.Null(HandStats.CountOuts(Card.ParseMany("Ah Kh"), []));
        Assert.Null(HandStats.CountOuts(Card.ParseMany("Ah Kh"), Card.ParseMany("Qh 7c 2h 3d 9s")));
        var turn = HandStats.CountOuts(Card.ParseMany("8s 7s"), Card.ParseMany("6d 5c Kh 2c"))!;
        Assert.Equal(HandCategory.HighCard, turn.Current);
        Assert.Equal(HandCategory.Straight, turn.Best);
        Assert.Equal(turn.Count / 46.0, turn.HitChance, 6);
    }

    [Fact]
    public void Starting_hands_are_ranked_sensibly()
    {
        var aa = StartingHands.RankOf(Card.Parse("As"), Card.Parse("Ah"));
        var kk = StartingHands.RankOf(Card.Parse("Ks"), Card.Parse("Kd"));
        var seventwo = StartingHands.RankOf(Card.Parse("7c"), Card.Parse("2d"));
        Assert.Equal(1, aa);
        Assert.Equal(2, kk);
        Assert.True(seventwo > 150);
        Assert.Equal("AKs", StartingHands.Key(Card.Parse("Kh"), Card.Parse("Ah")));
        Assert.Equal("T9o", StartingHands.Key(Card.Parse("9c"), Card.Parse("Td")));
        Assert.InRange(StartingHands.TopPercent(Card.Parse("As"), Card.Parse("Ad")), 0.004, 0.005);
        Assert.True(StartingHands.TopPercent(Card.Parse("7c"), Card.Parse("2d")) > 0.9);
        Assert.Equal(169, StartingHands.RankOf(Card.Parse("3c"), Card.Parse("2d")));
    }
}
