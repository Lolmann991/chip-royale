using ChipRoyale.Engine.Casino;
using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Engine.Tests;

public class RouletteTests
{
    [Fact]
    public void Wheel_has_every_number_once_and_18_red()
    {
        Assert.Equal(37, European.WheelOrder.Count);
        Assert.Equal(Enumerable.Range(0, 37), European.WheelOrder.Order());
        Assert.Equal(18, Enumerable.Range(1, 36).Count(European.IsRed));
        Assert.False(European.IsRed(0));
        Assert.False(European.IsBlack(0));
        Assert.True(European.IsRed(32)); // right of the zero
    }

    [Fact]
    public void Payouts_match_the_table()
    {
        Assert.Equal(360, European.Payout([European.Straight(17, 10)], 17));
        Assert.Equal(0, European.Payout([European.Straight(17, 10)], 18));
        Assert.Equal(180, European.Payout([European.Split(17, 20, 10)], 20));
        Assert.Equal(120, European.Payout([European.Street(6, 10)], 17)); // 16-17-18
        Assert.Equal(90, European.Payout([European.Corner(17, 10)], 21)); // 17-18-20-21
        Assert.Equal(60, European.Payout([European.SixLine(1, 10)], 6));
        Assert.Equal(30, European.Payout([European.Dozen(2, 10)], 24));
        Assert.Equal(30, European.Payout([European.Column(3, 10)], 36));
        Assert.Equal(20, European.Payout([European.Outside(RouletteBetKind.Red, 10)], 1));
        Assert.Equal(0, European.Payout([European.Outside(RouletteBetKind.Red, 10)], 0)); // zero loses outside bets
    }

    [Fact]
    public void Every_bet_has_the_same_house_edge()
    {
        RouletteBet[] bets =
        [
            European.Straight(7, 1), European.Split(0, 2, 1), European.Street(3, 1), European.Corner(1, 1), European.SixLine(5, 1),
            European.Dozen(3, 1), European.Column(1, 1), European.Outside(RouletteBetKind.Even, 1), European.Outside(RouletteBetKind.High, 1),
        ];
        foreach (var bet in bets)
        {
            var expected = Enumerable.Range(0, 37).Average(n => European.Payout([bet], n));
            Assert.Equal(36.0 / 37, expected, 9);
        }
    }

    [Fact]
    public void Invalid_splits_and_corners_are_refused()
    {
        Assert.Throws<ArgumentException>(() => European.Split(3, 4, 1)); // 3 is top of its column
        Assert.Throws<ArgumentException>(() => European.Split(1, 5, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => European.Corner(3, 1));
        Assert.Equal(new[] { 0, 3 }, European.Split(3, 0, 1).Numbers);
    }

    [Fact]
    public void Win_chance_counts_covered_numbers()
    {
        Assert.Equal(18 / 37.0, European.WinChance([European.Outside(RouletteBetKind.Red, 5)]), 9);
        Assert.Equal(19 / 37.0, European.WinChance([European.Outside(RouletteBetKind.Red, 5), European.Straight(0, 1), European.Straight(1, 1)]), 9);
    }

    [Fact]
    public void Casino_stats_and_daily_bonus()
    {
        var stats = new CasinoStats();
        stats.Record("roulette", 20, 340, ["straight"]);
        stats.Record("roulette", 20, -20, []);
        var copy = CasinoStats.Parse(stats.Serialize());
        Assert.Equal(2, copy.Get("roulette").Rounds);
        Assert.Equal(1, copy.Get("roulette").Won);
        Assert.Equal(320, copy.Get("roulette").Net);
        Assert.Equal(1, copy.HighlightCount("roulette", "straight"));

        var monday = new DateOnly(2026, 11, 23);
        Assert.True(stats.DailyAvailable(monday));
        stats.CollectDaily(monday);
        Assert.False(stats.DailyAvailable(monday));
        Assert.Equal(2, stats.StreakFor(monday.AddDays(1)));
        stats.CollectDaily(monday.AddDays(1));
        Assert.Equal(1, stats.StreakFor(monday.AddDays(3))); // a day missed
        Assert.Equal(110, DailyWheel.Payout(0, 2)); // 100 + 10 %
        Assert.Equal(200, DailyWheel.Payout(0, 30)); // at most double
        Assert.Equal("", new CasinoStats().Serialize());
    }
}
