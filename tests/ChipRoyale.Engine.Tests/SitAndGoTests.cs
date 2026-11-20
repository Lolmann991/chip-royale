using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Holdem;
using ChipRoyale.Engine.Tournament;

namespace ChipRoyale.Engine.Tests;

public class SitAndGoTests
{
    private static (HoldemTable Table, SitAndGo Sng) New(SitAndGoConfig? config = null, int seed = 3)
    {
        config ??= SitAndGoTiers.Find("bronze")!;
        var players = Enumerable.Range(0, config.Players).Select(i => new Player($"P{i}", config.StartingStack, i == 0));
        var table = new HoldemTable(players, 10, 20, new Random(seed));
        return (table, new SitAndGo(config, table));
    }

    [Fact]
    public void Prize_pool_is_fully_paid_out()
    {
        foreach (var t in SitAndGoTiers.All)
        {
            Assert.Equal(t.PrizePool, t.Prize(1) + t.Prize(2));
            Assert.Equal(0, t.Prize(3));
            Assert.True(t.Prize(1) > t.Prize(2));
        }
        Assert.Equal(780, SitAndGoTiers.Find("bronze")!.Prize(1));
    }

    [Fact]
    public void Blinds_rise_every_level()
    {
        var (table, sng) = New();
        var seen = new List<int>();
        for (var hand = 0; hand < 20; hand++)
        {
            var up = sng.BeforeHand();
            Assert.Equal(hand is 8 or 16, up);
            seen.Add(table.BigBlind);
            Assert.Equal(sng.Blinds.BigBlind, table.BigBlind);
            table.StartHand();
            while (table.Phase != TablePhase.HandComplete)
            {
                if (table.Phase == TablePhase.RoundComplete) table.Advance();
                else table.Act(PlayerAction.Fold());
            }
        }
        Assert.Equal(20, seen[0]);
        Assert.Equal(20, seen[7]);
        Assert.Equal(30, seen[8]);
        Assert.Equal(50, seen[16]);
    }

    [Fact]
    public void Hands_left_in_level_counts_down()
    {
        var (_, sng) = New();
        sng.BeforeHand();
        Assert.Equal(8, sng.HandsLeftInLevel);
        for (var i = 0; i < 7; i++) sng.BeforeHand();
        Assert.Equal(1, sng.HandsLeftInLevel);
        sng.BeforeHand();
        Assert.Equal(1, sng.Level);
        Assert.Equal(8, sng.HandsLeftInLevel);
    }

    [Fact]
    public void Tournament_with_bots_ends_with_unique_places()
    {
        var rng = new Random(11);
        var (table, sng) = New(seed: 11);
        var brains = table.Players.Select(_ => new BotBrain(new BotProfile(Difficulty.Easy, rng.NextDouble(), rng.NextDouble()), rng) { Iterations = 50 }).ToList();
        var guard = 0;
        while (!sng.IsFinished)
        {
            Assert.True(guard++ < 2000, "Tournament did not finish.");
            sng.BeforeHand();
            table.StartHand();
            while (table.Phase != TablePhase.HandComplete)
            {
                if (table.Phase == TablePhase.RoundComplete) table.Advance();
                else table.Act(brains[table.ActingIndex!.Value].Decide(table));
            }
            sng.AfterHand();
            Assert.Equal(6 * 1500, table.Players.Sum(p => p.Chips));
        }

        Assert.Equal(6, sng.Places.Count);
        Assert.Equal(Enumerable.Range(1, 6), sng.Places.Values.Order());
        var winner = sng.Places.Single(p => p.Value == 1).Key;
        Assert.Equal(9000, table.Players[winner].Chips);
    }

    [Fact]
    public void Bankroll_books_buy_in_and_prize()
    {
        var b = Bankroll.New();
        b.PayTournamentBuyIn(200);
        Assert.Equal(800, b.Chips);
        b.RecordTournament(1, 780);
        Assert.Equal(1580, b.Chips);
        Assert.Equal(1, b.TournamentsWon);
        Assert.Equal(1, b.TournamentsPlayed);
        Assert.False(Bankroll.New(100).CanEnterTournament(200));
    }
}
