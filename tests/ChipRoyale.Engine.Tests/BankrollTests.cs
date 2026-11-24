using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Engine.Tests;

public class BankrollTests
{
    private static TableStake Pub => TableStakes.Find("pub")!;
    private static TableStake Casino => TableStakes.Find("casino")!;

    [Fact]
    public void Max_buy_in_is_twenty_times_min_except_no_limit_tables()
    {
        Assert.All(TableStakes.Real.Where(s => !s.NoLimit), s => Assert.Equal(s.MinBuyIn * 20, s.MaxBuyIn));
    }

    [Fact]
    public void Buy_in_moves_chips_to_the_table_and_cash_out_back()
    {
        var b = Bankroll.New();
        b.BuyIn(Pub, 400);
        Assert.Equal(600, b.Chips);
        Assert.Equal(400, b.TableChips);
        Assert.Equal(1000, b.Total);

        b.SetTableStack(900);
        b.CashOut();
        Assert.Equal(1500, b.Chips);
        Assert.Equal(0, b.TableChips);
        Assert.Equal(1500, b.BestPeak);
    }

    [Fact]
    public void Buy_in_outside_limits_is_rejected()
    {
        var b = Bankroll.New();
        Assert.Throws<ArgumentOutOfRangeException>(() => b.BuyIn(Pub, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.BuyIn(Pub, 1200)); // more than owned
        Assert.False(Bankroll.New(500).CanSit(Casino));
    }

    [Fact]
    public void Cannot_sit_at_two_tables()
    {
        var b = Bankroll.New();
        b.BuyIn(Pub, 200);
        Assert.False(b.CanSit(Pub));
        Assert.Throws<InvalidOperationException>(() => b.BuyIn(Pub, 200));
    }

    [Fact]
    public void Practice_table_does_not_touch_chips()
    {
        var b = Bankroll.New();
        b.BuyIn(TableStakes.Practice, 1000);
        Assert.Equal(1000, b.Chips);
        Assert.False(b.IsSeated);
    }

    [Fact]
    public void Going_broke_starts_a_new_run_but_keeps_best_peak()
    {
        var b = Bankroll.New();
        b.BuyIn(Pub, 1000);
        b.SetTableStack(3000);
        b.SetTableStack(0);
        Assert.True(b.IsBroke);

        Assert.True(b.ResetIfBroke());
        Assert.Equal(2, b.Run);
        Assert.Equal(1, b.Busts);
        Assert.Equal(TableStakes.StartingChips, b.Chips);
        Assert.Equal(TableStakes.StartingChips, b.RunPeak);
        Assert.Equal(3000, b.BestPeak);
    }

    [Fact]
    public void Less_than_cheapest_buy_in_counts_as_broke()
    {
        var b = Bankroll.New();
        b.BuyIn(Pub, 900);
        b.SetTableStack(50);
        b.CashOut();
        Assert.Equal(150, b.Chips);
        Assert.True(b.IsBroke);
        Assert.False(Bankroll.New().ResetIfBroke());
    }
}
