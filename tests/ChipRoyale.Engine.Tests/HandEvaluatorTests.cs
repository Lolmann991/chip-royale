using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Tests;

public class HandEvaluatorTests
{
    private static HandValue Best(string cards) => HandEvaluator.EvaluateBest(Card.ParseMany(cards));

    [Theory]
    [InlineData("As Ks Qs Js Ts", HandCategory.StraightFlush)]
    [InlineData("5d 4d 3d 2d Ad", HandCategory.StraightFlush)]
    [InlineData("9c 9d 9h 9s 2c", HandCategory.FourOfAKind)]
    [InlineData("Kc Kd Kh 4s 4c", HandCategory.FullHouse)]
    [InlineData("Ah 9h 7h 4h 2h", HandCategory.Flush)]
    [InlineData("9c 8d 7h 6s 5c", HandCategory.Straight)]
    [InlineData("Ac 2d 3h 4s 5c", HandCategory.Straight)]
    [InlineData("7c 7d 7h Ks 2c", HandCategory.ThreeOfAKind)]
    [InlineData("Jc Jd 4h 4s 2c", HandCategory.TwoPair)]
    [InlineData("Qc Qd 9h 4s 2c", HandCategory.OnePair)]
    [InlineData("Ac Jd 9h 4s 2c", HandCategory.HighCard)]
    public void Detects_category(string cards, HandCategory expected) =>
        Assert.Equal(expected, Best(cards).Category);

    [Fact]
    public void Picks_best_five_of_seven()
    {
        var value = Best("As Ks 2d 7c Qs Js Ts");
        Assert.Equal(HandCategory.StraightFlush, value.Category);
        Assert.Equal(Rank.Ace, value.Kickers[0]);
    }

    [Fact]
    public void Wheel_loses_to_six_high_straight() =>
        Assert.True(Best("Ac 2d 3h 4s 5c") < Best("2c 3d 4h 5s 6c"));

    [Fact]
    public void Kicker_decides_between_equal_pairs() =>
        Assert.True(Best("Ac Ad Kh 7s 2c") > Best("As Ah Qh 7d 2d"));

    [Fact]
    public void Identical_strength_is_a_split()
    {
        var a = Best("Ac Kd Qh Js 9c");
        var b = Best("Ad Kh Qs Jc 9d");
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void Higher_full_house_wins_by_trips_rank() =>
        Assert.True(Best("3c 3d 3h 2s 2c") > Best("2d 2h 2s Ac Ad"));

    [Fact]
    public void Omaha_must_use_exactly_two_hole_cards()
    {
        // Four hearts on board + one heart in hand is NOT a flush in Omaha.
        var hole = Card.ParseMany("Ah Kc Qd Js");
        var board = Card.ParseMany("2h 5h 8h Th 3c");
        var value = HandEvaluator.EvaluateOmaha(hole, board);
        Assert.NotEqual(HandCategory.Flush, value.Category);
    }
}
