using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Casino;
using Xunit.Abstractions;

namespace ChipRoyale.Engine.Tests;

public class VideoPokerTests(ITestOutputHelper output)
{
    private static VpHand Eval(string cards) => JacksOrBetter.Evaluate(Card.ParseMany(cards));

    [Fact]
    public void Hands_are_recognised()
    {
        Assert.Equal(VpHand.RoyalFlush, Eval("Th Jh Qh Kh Ah"));
        Assert.Equal(VpHand.StraightFlush, Eval("9h Th Jh Qh Kh"));
        Assert.Equal(VpHand.FourOfAKind, Eval("9h 9d 9c 9s 2h"));
        Assert.Equal(VpHand.FullHouse, Eval("9h 9d 9c 2s 2h"));
        Assert.Equal(VpHand.Flush, Eval("2h 5h 9h Jh Kh"));
        Assert.Equal(VpHand.Straight, Eval("Ah 2d 3c 4s 5h"));
        Assert.Equal(VpHand.ThreeOfAKind, Eval("9h 9d 9c 2s 3h"));
        Assert.Equal(VpHand.TwoPair, Eval("9h 9d 2c 2s 3h"));
        Assert.Equal(VpHand.JacksOrBetter, Eval("Jh Jd 2c 5s 8h"));
        Assert.Equal(VpHand.Nothing, Eval("Th Td 2c 5s 8h"));
        Assert.Equal(800, JacksOrBetter.Pays(VpHand.RoyalFlush));
        Assert.Equal(0, JacksOrBetter.Pays(VpHand.Nothing));
    }

    [Theory]
    [InlineData("Th Jh Qh Kh 2c", "11110", "FourToRoyal")]
    [InlineData("Th Jh Qh Kh Ac", "11110", "FourToRoyal")] // break the straight for the royal draw
    [InlineData("9h 9d 9c 2s 3h", "11100", "MadeHand")]
    [InlineData("Jh Jd 2c 5s 8h", "11000", "HighPair")]
    [InlineData("2h 2d Kc 5s 8h", "11000", "LowPair")]
    [InlineData("2h 5h 9h Jh Kc", "11110", "FourToFlush")]
    [InlineData("5h 6d 7c 8s Kh", "11110", "FourToOutsideStraight")]
    [InlineData("Jh Qh 2c 5s 8d", "11000", "TwoSuitedHigh")]
    [InlineData("Jh Qd Kc 5s 8d", "11000", "TwoHigh")] // the two lowest high cards
    [InlineData("2h 4d 7c 9s 3c", "00000", "Nothing")]
    public void Hints_follow_the_simple_strategy(string cards, string hold, string reason)
    {
        var advice = JacksOrBetter.Advise(Card.ParseMany(cards));
        Assert.Equal(hold, string.Concat(advice.Hold.Select(h => h ? '1' : '0')));
        Assert.Equal(reason, advice.Reason.ToString());
    }

    [Fact]
    public void Draw_replaces_only_unheld_cards()
    {
        var hand = new VideoPokerHand(5, Card.ParseMany("Jh Jd 2c 5s 8h"), Card.ParseMany("Jc 3d 4d"));
        hand.Toggle(0);
        hand.Toggle(1);
        hand.Draw();
        Assert.Equal(Card.ParseMany("Jh Jd Jc 3d 4d"), hand.Cards);
        Assert.Equal(VpHand.ThreeOfAKind, hand.Hand);
        Assert.Equal(15, hand.Payout);
    }

    [Fact]
    public void Playing_the_hints_returns_about_99_percent()
    {
        var random = new Random(11);
        long paid = 0, won = 0;
        const int hands = 200_000;
        for (var i = 0; i < hands; i++)
        {
            var hand = new VideoPokerHand(1, random);
            var advice = JacksOrBetter.Advise(hand.Cards);
            for (var c = 0; c < 5; c++) if (advice.Hold[c]) hand.Toggle(c);
            hand.Draw();
            paid += 1;
            won += hand.Payout;
        }
        var rtp = won / (double)paid;
        output.WriteLine($"Video poker return with hints: {rtp:P2}");
        Assert.InRange(rtp, 0.95, 1.04);
    }
}
