using ChipRoyale.Engine.Economy;
using ChipRoyale.Shared;

namespace ChipRoyale.Client.Tests;

/// <summary>The rules shared by the server (database) and the offline profile in the browser.</summary>
public class ProfileOperationsTests
{
    private static TableStake Stake => TableStakes.All.First(s => !s.IsPractice);

    [Fact]
    public void Buy_in_and_cash_out_move_the_chips()
    {
        var state = PlayerState.New("Test");
        var start = state.Bankroll.Chips;
        Assert.Null(ProfileOperations.BuyIn(state, Stake.Id, Stake.MinBuyIn).Error);
        Assert.Equal(Stake.MinBuyIn, state.Bankroll.TableChips);

        ProfileOperations.Table(state, Stake.MinBuyIn + 50, new HandOutcomeDto(true, 120, false, null));
        ProfileOperations.CashOut(state, Stake.MinBuyIn + 50);
        Assert.Equal(start + 50, state.Bankroll.Chips);
        Assert.Equal(1, state.Bankroll.HandsWon);
    }

    [Fact]
    public void Invalid_requests_are_refused_without_changes()
    {
        var state = PlayerState.New("Test");
        var before = state.Bankroll.ToState();
        Assert.NotNull(ProfileOperations.BuyIn(state, "gibt-es-nicht", 100).Error);
        Assert.NotNull(ProfileOperations.BuyIn(state, Stake.Id, Stake.MaxBuyIn * 100).Error);
        Assert.NotNull(ProfileOperations.Buy(state, "gibt-es-nicht").Error);
        Assert.Equal(before, state.Bankroll.ToState());
    }

    [Fact]
    public void Profile_survives_the_json_round_trip()
    {
        var state = PlayerState.New("Test");
        ProfileOperations.BuyIn(state, Stake.Id, Stake.MinBuyIn);
        var json = System.Text.Json.JsonSerializer.Serialize(state.ToDto());
        var copy = PlayerState.FromDto(System.Text.Json.JsonSerializer.Deserialize<ProfileDto>(json)!);
        Assert.Equal(state.Bankroll.ToState(), copy.Bankroll.ToState());
        Assert.Equal("Test", copy.DisplayName);
    }

    [Fact]
    public void A_closed_tab_returns_the_table_chips_on_load()
    {
        var state = PlayerState.New("Test");
        ProfileOperations.BuyIn(state, Stake.Id, Stake.MinBuyIn);
        var result = ProfileOperations.Load(state);
        Assert.Equal(Stake.MinBuyIn, result.ReturnedFromTable);
        Assert.Equal(0, state.Bankroll.TableChips);
    }

    [Fact]
    public void Blackjack_rounds_move_the_stack_and_count_statistics()
    {
        var state = PlayerState.New("Test");
        Assert.Null(ProfileOperations.BuyIn(state, "bj-pub", 500).Error);
        Assert.Equal(500, state.Bankroll.TableChips);

        var result = ProfileOperations.Blackjack(state, 575, new BlackjackRoundDto(75, Natural: true, DoubleWon: false, SplitWon: false));
        Assert.Contains("bj-natural", result.Unlocked);
        Assert.Equal(1, state.Bankroll.BlackjackRounds);
        Assert.Equal(1, state.Bankroll.BlackjackNaturals);
        Assert.Equal(75, state.Bankroll.BlackjackBiggestWin);
        Assert.Equal(0, state.Bankroll.HandsPlayed); // poker statistics stay untouched

        ProfileOperations.CashOut(state, 575);
        Assert.Equal(1075 + 3, state.Bankroll.Chips + state.Bankroll.Crowns); // 1075 chips, 3 crowns reward
    }

    [Fact]
    public void Casino_rounds_count_per_game_and_unlock_achievements()
    {
        var state = PlayerState.New("Test");
        Assert.Null(ProfileOperations.BuyIn(state, "roulette-pub", 200).Error);
        var r = ProfileOperations.CasinoRound(state, 550, new CasinoRoundDto("roulette", 10, 350, ["straight"]));
        Assert.Contains("roulette-straight", r.Unlocked);
        Assert.Equal(550, state.Bankroll.TableChips);
        Assert.Equal(1, state.Casino.Get("roulette").Rounds);
        Assert.Equal(350, state.Casino.Get("roulette").Biggest);
        Assert.NotNull(ProfileOperations.CasinoRound(state, 1, new CasinoRoundDto("craps", 1, 1, [])).Error); // unknown game

        // all five games → all-rounder
        state.Bankroll.RecordHand(true, 10);
        state.Bankroll.RecordBlackjack(10, false);
        ProfileOperations.CasinoRound(state, 550, new CasinoRoundDto("slots", 10, -10, []));
        var last = ProfileOperations.CasinoRound(state, 550, new CasinoRoundDto("videopoker", 10, -10, []));
        Assert.Contains("allrounder", last.Unlocked);
        Assert.Equal(5, state.GamesPlayed);
    }

    [Fact]
    public void Daily_bonus_once_per_day_with_streak()
    {
        var state = PlayerState.New("Test");
        var day = new DateOnly(2026, 11, 24);
        var first = ProfileOperations.DailyBonus(state, day, new Random(3));
        Assert.Null(first.Error);
        Assert.NotNull(first.Value);
        var chips = state.Bankroll.Chips;
        Assert.True(chips > 1000);
        Assert.Equal("Daily.Taken", ProfileOperations.DailyBonus(state, day, new Random(3)).Error);
        Assert.Equal(chips, state.Bankroll.Chips);

        for (var d = 1; d < 7; d++) ProfileOperations.DailyBonus(state, day.AddDays(d), new Random(d));
        Assert.Equal(7, state.Casino.DailyStreak);
        Assert.Contains("daily-7", state.Collection.Achievements);

        // the casino data survives the JSON round trip (server and browser)
        var copy = PlayerState.FromDto(System.Text.Json.JsonSerializer.Deserialize<ProfileDto>(System.Text.Json.JsonSerializer.Serialize(state.ToDto()))!);
        Assert.Equal(7, copy.Casino.DailyStreak);
    }
}
