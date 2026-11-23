using ChipRoyale.Engine.Blackjack;
using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Tests;

public class BlackjackTests
{
    /// <summary>Cards in dealing order: player, dealer up, player, dealer hole, then the rest.</summary>
    private static BlackjackRound Round(string cards, int stack = 1000) =>
        new(Shoe.Stacked(Card.ParseMany(cards)), BlackjackRules.Standard, stack);

    [Fact]
    public void Totals_count_aces_as_one_or_eleven()
    {
        Assert.Equal((21, true), BjValue.Of(Card.ParseMany("Ah Kd")));
        Assert.Equal((12, false), BjValue.Of(Card.ParseMany("Ah Kd As")));
        Assert.Equal((12, true), BjValue.Of(Card.ParseMany("Ah Ad")));
        Assert.Equal((21, false), BjValue.Of(Card.ParseMany("9h 5d 7c")));
    }

    [Fact]
    public void Natural_pays_three_to_two()
    {
        var r = Round("Ah 9c Kd 7s");
        r.Deal(100);
        Assert.Equal(BjPhase.Finished, r.Phase);
        Assert.Equal(BjOutcome.Blackjack, r.Hands[0].Outcome);
        Assert.Equal(1150, r.Stack);
        Assert.True(r.Result!.Natural);
    }

    [Fact]
    public void Dealer_blackjack_ends_the_round_and_pushes_a_player_blackjack()
    {
        var lose = Round("Th Ac 9d Ks");
        lose.Deal(50);
        Assert.Equal(BjPhase.Finished, lose.Phase);
        Assert.Equal(BjOutcome.Lose, lose.Hands[0].Outcome);
        Assert.Equal(950, lose.Stack);

        var push = Round("Ah Ac Kd Ks");
        push.Deal(50);
        Assert.Equal(BjOutcome.Push, push.Hands[0].Outcome);
        Assert.Equal(1000, push.Stack);
    }

    [Fact]
    public void Hit_stand_and_dealer_stands_on_soft_17()
    {
        // Player 10+6, dealer 6 + A (soft 17 → stands). Player hits a 3 → 19 and wins.
        var r = Round("Th 6c 6d As 3h");
        r.Deal(100);
        r.Act(BjAction.Hit);
        Assert.Equal(19, r.Hands[0].Total);
        r.Act(BjAction.Stand);
        Assert.Equal(BjPhase.DealerTurn, r.Phase);
        Assert.False(r.DealerMustDraw);
        r.DealerStands();
        Assert.Equal(BjOutcome.Win, r.Hands[0].Outcome);
        Assert.Equal(1100, r.Stack);
    }

    [Fact]
    public void Bust_loses_without_the_dealer_drawing()
    {
        var r = Round("Th 6c 6d 9s Kh");
        r.Deal(100);
        r.Act(BjAction.Hit);
        Assert.Equal(BjPhase.Finished, r.Phase);
        Assert.Equal(BjOutcome.Bust, r.Hands[0].Outcome);
        Assert.Equal(2, r.Dealer.Count);
        Assert.Equal(900, r.Stack);
    }

    [Fact]
    public void Double_takes_one_card_and_pays_double()
    {
        // 6+5 = 11, double → K = 21; dealer 9 + 7 = 16 draws 8 → bust.
        var r = Round("6h 9c 5d 7s Kh 8d");
        r.Deal(100);
        Assert.True(r.CanDouble);
        r.Act(BjAction.Double);
        Assert.Equal(200, r.Hands[0].Bet);
        Assert.Equal(BjPhase.DealerTurn, r.Phase);
        r.PlayDealer();
        Assert.Equal(BjOutcome.Win, r.Hands[0].Outcome);
        Assert.Equal(1200, r.Stack);
        Assert.True(r.Result!.DoubleWon);
    }

    [Fact]
    public void Split_plays_two_hands()
    {
        // 8+8 vs dealer 6 (+ 10 = 16). Split: first hand gets 3 (11), second gets T (18).
        var r = Round("8h 6c 8d Ts 3h Td Kc 9h");
        r.Deal(100);
        Assert.True(r.CanSplit);
        r.Act(BjAction.Split);
        Assert.Equal(2, r.Hands.Count);
        Assert.Equal(800, r.Stack);
        Assert.Equal(11, r.Hands[0].Total);
        r.Act(BjAction.Double); // 11 + T = 21 (no blackjack after a split)
        Assert.Equal(21, r.Hands[0].Total);
        Assert.False(r.Hands[0].IsBlackjack);
        Assert.Equal(1, r.ActiveIndex);
        Assert.Equal(18, r.Hands[1].Total);
        r.Act(BjAction.Stand);
        r.PlayDealer(); // 16 + K = bust
        Assert.All(r.Hands, h => Assert.Equal(BjOutcome.Win, h.Outcome));
        Assert.Equal(700 + 400 + 200, r.Stack);
        Assert.True(r.Result!.SplitWon);
    }

    [Fact]
    public void Split_aces_get_one_card_each()
    {
        var r = Round("Ah 9c Ad 7s 5h Kd");
        r.Deal(100);
        r.Act(BjAction.Split);
        Assert.Equal(BjPhase.DealerTurn, r.Phase);
        Assert.Equal(16, r.Hands[0].Total);
        Assert.Equal(21, r.Hands[1].Total);
        Assert.False(r.Hands[1].IsBlackjack);
    }

    [Fact]
    public void Cannot_bet_or_double_more_than_the_stack()
    {
        var r = Round("6h 9c 5d 7s", stack: 150);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Deal(200));
        r.Deal(100);
        Assert.False(r.CanDouble);
    }

    [Fact]
    public void Basic_strategy_spot_checks()
    {
        BjAction Advice(string hand, string up, bool canDouble = true, bool canSplit = true)
        {
            var h = new BjHand();
            h.Cards.AddRange(Card.ParseMany(hand));
            return BasicStrategy.Suggest(h, Card.Parse(up), canDouble, canSplit).Action;
        }
        Assert.Equal(BjAction.Split, Advice("8h 8d", "Ts"));
        Assert.Equal(BjAction.Split, Advice("Ah Ad", "6s"));
        Assert.Equal(BjAction.Stand, Advice("Th Td", "6s"));
        Assert.Equal(BjAction.Double, Advice("6h 5d", "Ts"));
        Assert.Equal(BjAction.Hit, Advice("6h 5d", "As"));
        Assert.Equal(BjAction.Stand, Advice("Th 3d", "4s"));
        Assert.Equal(BjAction.Hit, Advice("Th 6d", "7s"));
        Assert.Equal(BjAction.Double, Advice("Ah 7d", "5s"));
        Assert.Equal(BjAction.Stand, Advice("Ah 7d", "5s", canDouble: false));
        Assert.Equal(BjAction.Hit, Advice("Ah 7d", "9s"));
        Assert.Equal(BjAction.Hit, Advice("8h 8d", "Ts", canSplit: false));
    }

    [Fact]
    public void Bust_chance_and_card_counting()
    {
        var shoe = new Shoe(1, new Random(3));
        var full = shoe.RemainingByValue();
        Assert.Equal(16, full[10]);
        var hand = new BjHand();
        hand.Cards.AddRange(Card.ParseMany("Th 6d"));
        // 16 busts with 6, 7, 8, 9, 10: (4·4 + 16) / 52 when nothing is dealt
        Assert.Equal((16 + 16) / 52.0, BasicStrategy.BustChance(hand, full), 6);
        var soft = new BjHand();
        soft.Cards.AddRange(Card.ParseMany("Ah 6d"));
        Assert.Equal(0, BasicStrategy.BustChance(soft, full));

        for (var i = 0; i < 52; i++) shoe.Draw();
        Assert.Equal(0, shoe.RunningCount); // a full deck counts to zero
    }
}
