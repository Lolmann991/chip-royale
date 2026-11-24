using ChipRoyale.Engine.Blackjack;
using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Casino;
using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Engine.Tests;

/// <summary>VIP tables without limit: huge bets must never overflow.</summary>
public class NoLimitTests
{
    [Fact]
    public void Vip_tables_have_no_limit()
    {
        var bj = BlackjackTables.Find("bj-vip")!;
        Assert.True(bj.NoLimit);
        Assert.Equal(int.MaxValue, bj.MaxBet);
        Assert.Equal(int.MaxValue, bj.MaxBuyIn);
        Assert.True(CasinoTables.Find("roulette-vip")!.NoLimit);
        Assert.False(CasinoTables.Find("roulette-casino")!.NoLimit);
        Assert.Equal(250, CasinoTables.Find("slots-casino")!.MaxBet);

        // Poker keeps a (very high) cap: six stacks have to fit into one number.
        var poker = TableStakes.Find("vip")!;
        Assert.Equal(250_000_000, poker.MaxBuyIn);
        Assert.True(6L * poker.MaxBuyIn < int.MaxValue);
    }

    [Fact]
    public void Buy_in_of_a_billion_at_a_no_limit_table()
    {
        var b = Bankroll.FromState(Bankroll.New().ToState() with { Chips = 1_500_000_000 });
        var vip = BlackjackTables.Find("bj-vip")!;
        Assert.Equal(1_500_000_000, b.MaxBuyInFor(vip));
        b.BuyIn(vip, 1_400_000_000);
        b.SetTableStack(2_100_000_000);
        b.CashOut();
        Assert.Equal(int.MaxValue, b.Chips); // 100 M + 2.1 bn would overflow: capped at the account maximum
        Assert.Equal(int.MaxValue, b.Total);
    }

    [Fact]
    public void Blackjack_with_a_billion_bet_pays_without_overflow()
    {
        var round = new BlackjackRound(Shoe.Stacked(Card.ParseMany("Ah 9c Kd 7s")), BlackjackRules.Standard, 2_000_000_000);
        round.Deal(1_000_000_000);
        Assert.Equal(BjOutcome.Blackjack, round.Hands[0].Outcome);
        Assert.Equal(2_500_000_000L, round.Hands[0].Payout);
        Assert.Equal(int.MaxValue, round.Stack); // 1 bn + 2.5 bn, capped
        Assert.Equal(1_500_000_000L, round.Result!.Net);
    }

    [Fact]
    public void Big_wins_in_roulette_slots_and_video_poker_are_long()
    {
        Assert.Equal(36_000_000_000L, European.Payout([European.Straight(7, 1_000_000_000)], 7));
        var hand = new VideoPokerHand(1_000_000_000, Card.ParseMany("Th Jh Qh Kh Ah"), []);
        hand.Draw();
        Assert.Equal(800_000_000_000L, hand.Payout);
        Assert.Equal(int.MaxValue, Chips.Add(5, hand.Payout));
        Assert.Equal(0, Chips.Add(5, -10));
    }
}
