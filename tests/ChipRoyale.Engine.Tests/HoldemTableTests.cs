using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Engine.Holdem;

namespace ChipRoyale.Engine.Tests;

public class HoldemTableTests
{
    private static HoldemTable NewTable(params int[] stacks) =>
        new(stacks.Select((c, i) => new Player($"P{i}", c, i == 0)), 10, 20, new Random(7));

    [Fact]
    public void Blinds_are_posted_and_cards_dealt()
    {
        var t = NewTable(1000, 1000, 1000);
        t.StartHand();
        Assert.Equal(30, t.Pot);
        Assert.All(t.Players, p => Assert.Equal(2, p.HoleCards.Count));
        Assert.Equal(10, t.Players[t.SmallBlindIndex].StreetBet);
        Assert.Equal(20, t.Players[t.BigBlindIndex].StreetBet);
        Assert.Equal(TablePhase.WaitingForAction, t.Phase);
        // Under the gun is left of the big blind; with 3 players that's the dealer.
        Assert.Equal(t.DealerIndex, t.ActingIndex);
    }

    [Fact]
    public void Heads_up_dealer_posts_small_blind_and_acts_first()
    {
        var t = NewTable(1000, 1000);
        t.StartHand();
        Assert.Equal(t.DealerIndex, t.SmallBlindIndex);
        Assert.Equal(t.DealerIndex, t.ActingIndex);
    }

    [Fact]
    public void Everyone_folds_to_big_blind_who_wins_blinds()
    {
        var t = NewTable(1000, 1000, 1000);
        t.StartHand();
        var bb = t.BigBlindIndex;
        while (t.Phase == TablePhase.WaitingForAction)
            t.Act(PlayerAction.Fold());

        Assert.Equal(TablePhase.HandComplete, t.Phase);
        Assert.Equal(1010, t.Players[bb].Chips);
        Assert.False(t.LastResult!.WasShowdown);
        Assert.Equal(3000, t.Players.Sum(p => p.Chips));
    }

    [Fact]
    public void Big_blind_gets_the_option_after_limps()
    {
        var t = NewTable(1000, 1000, 1000);
        t.StartHand();
        t.Act(PlayerAction.Call()); // dealer limps
        t.Act(PlayerAction.Call()); // small blind completes
        Assert.Equal(t.BigBlindIndex, t.ActingIndex);
        Assert.True(t.GetLegalActions().CanCheck);
        t.Act(PlayerAction.Check());
        Assert.Equal(TablePhase.RoundComplete, t.Phase);
        t.Advance();
        Assert.Equal(Street.Flop, t.Street);
        Assert.Equal(3, t.Board.Count);
    }

    [Fact]
    public void Uncalled_bet_is_returned()
    {
        var t = NewTable(1000, 1000);
        t.StartHand();
        var raiser = t.ActingIndex!.Value;
        t.Act(PlayerAction.AllIn());
        t.Act(PlayerAction.Fold());
        Assert.Equal(1020, t.Players[raiser].Chips);
    }

    [Fact]
    public void Side_pot_goes_to_the_right_players()
    {
        // P0 short stack all-in, P1 and P2 play for a side pot.
        var t = NewTable(100, 1000, 1000);
        t.StartHand();
        while (t.Phase != TablePhase.HandComplete)
        {
            if (t.Phase == TablePhase.RoundComplete) { t.Advance(); continue; }
            t.Act(PlayerAction.AllIn());
        }

        var result = t.LastResult!;
        Assert.True(result.WasShowdown);
        Assert.Equal(2100, result.TotalPot);
        Assert.Equal(2, result.Pots.Count);
        Assert.Equal(300, result.Pots[0].Amount);
        Assert.Equal(3, result.Pots[0].Eligible.Count);
        Assert.Equal(1800, result.Pots[1].Amount);
        Assert.DoesNotContain(0, result.Pots[1].Eligible);
        Assert.Equal(2100, t.Players.Sum(p => p.Chips));
    }

    [Fact]
    public void Too_small_raise_is_clamped_to_min_raise()
    {
        var t = NewTable(1000, 1000, 1000);
        t.StartHand();
        var applied = t.Act(PlayerAction.RaiseTo(25));
        Assert.Equal(ActionType.Raise, applied.Type);
        Assert.Equal(40, applied.Amount);
    }

    [Theory]
    [InlineData(Difficulty.Easy)]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    public void Bots_play_many_hands_without_losing_chips(Difficulty difficulty)
    {
        var rng = new Random(123);
        var players = Enumerable.Range(0, 6).Select(i => new Player($"Bot{i}", 1000)).ToList();
        var brains = players.Select(_ => new BotBrain(new BotProfile(difficulty, rng.NextDouble(), rng.NextDouble()), rng) { Iterations = 60 }).ToList();
        var t = new HoldemTable(players, 10, 20, rng);

        for (var hand = 0; hand < 150 && t.CanStartHand; hand++)
        {
            t.StartHand();
            var guard = 0;
            while (t.Phase != TablePhase.HandComplete)
            {
                Assert.True(guard++ < 200, "Hand did not finish.");
                if (t.Phase == TablePhase.RoundComplete) t.Advance();
                else t.Act(brains[t.ActingIndex!.Value].Decide(t));
            }
            Assert.Equal(6000, t.Players.Sum(p => p.Chips));
            Assert.All(t.Players, p => Assert.True(p.Chips >= 0));
        }
    }

    [Fact]
    public void Easy_bots_call_big_bets_more_often_than_hard_bots()
    {
        static int Calls(Difficulty d)
        {
            var rng = new Random(99);
            var calls = 0;
            for (var i = 0; i < 300; i++)
            {
                var t = new HoldemTable([new Player("You", 1000, true), new Player("Bot", 1000)], 10, 20, rng);
                t.StartHand();
                // Get to the flop, then the human bets the pot.
                while (t.Street == Street.Preflop)
                {
                    if (t.Phase == TablePhase.RoundComplete) { t.Advance(); break; }
                    t.Act(PlayerAction.Call());
                }
                if (t.Phase != TablePhase.WaitingForAction) continue;
                if (!t.Players[t.ActingIndex!.Value].IsHuman) t.Act(PlayerAction.Check());
                t.Act(PlayerAction.RaiseTo(t.Pot));
                var bot = new BotBrain(new BotProfile(d), rng) { Iterations = 150 };
                if (bot.Decide(t).Type != ActionType.Fold) calls++;
            }
            return calls;
        }

        Assert.True(Calls(Difficulty.Easy) > Calls(Difficulty.Hard) + 40);
    }
}

public class EquityTests
{
    [Fact]
    public void Aces_are_big_favourites_heads_up()
    {
        var eq = EquityCalculator.Estimate(Card.ParseMany("As Ah"), [], 1, 4000, new Random(1));
        Assert.InRange(eq, 0.80, 0.89);
    }

    [Fact]
    public void Made_royal_flush_always_wins()
    {
        var eq = EquityCalculator.Estimate(Card.ParseMany("As Ks"), Card.ParseMany("Qs Js Ts 2d 3c"), 3, 500, new Random(1));
        Assert.Equal(1.0, eq);
    }

    [Fact]
    public void Fast_evaluator_orders_hands_like_reference_evaluator()
    {
        var rng = new Random(5);
        var deck = new Deck(rng);
        for (var i = 0; i < 3000; i++)
        {
            deck.Reset();
            deck.Shuffle();
            var a = deck.Draw(7);
            var b = deck.Draw(7);
            var expected = Math.Sign(HandEvaluator.EvaluateBest(a).CompareTo(HandEvaluator.EvaluateBest(b)));
            var actual = Math.Sign(FastEvaluator.Score(a.ToArray()).CompareTo(FastEvaluator.Score(b.ToArray())));
            Assert.Equal(expected, actual);
            Assert.Equal(HandEvaluator.EvaluateBest(a).Category, FastEvaluator.CategoryOf(FastEvaluator.Score(a.ToArray())));
        }
    }
}
