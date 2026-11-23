using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Tournament;
using ChipRoyale.Client.Services;
using static ChipRoyale.Client.Localization.Loc;

namespace ChipRoyale.Client.Components.Game;

public enum GameKind
{
    Poker,
    Blackjack,
}

/// <param name="BuyIn">Chips the player brings to the table.</param>
/// <param name="Tournament">Set for a Sit &amp; Go; then <see cref="BuyIn"/> is the tournament buy-in.</param>
public sealed record GameSettings(Difficulty Difficulty, int Opponents, TableStake Stake, int BuyIn, SitAndGoConfig? Tournament = null,
    GameKind Game = GameKind.Poker)
{
    public bool IsBlackjack => Game == GameKind.Blackjack;

    public int SmallBlind => Stake.SmallBlind;
    public int BigBlind => Stake.BigBlind;
    public bool IsPractice => Stake.IsPractice;
    public bool IsTournament => Tournament is not null;

    public static GameSettings ForSitAndGo(Difficulty difficulty, SitAndGoConfig config) => new(
        difficulty,
        config.Players - 1,
        new TableStake("sng", "Sit & Go", config.Levels[0].SmallBlind, config.Levels[0].BigBlind, config.StartingStack),
        config.BuyIn,
        config);
}

/// <param name="Place">Final place (1 = winner).</param>
public readonly record struct TournamentResult(int Place, int Prize);

/// <param name="Stack">The player's chips at the table right now.</param>
/// <param name="Hand">Set when a hand the player was dealt into has finished.</param>
public readonly record struct TableReport(int Stack, HandOutcome? Hand);

/// <param name="Stack">Chips at the blackjack table after the round.</param>
public readonly record struct BlackjackReport(int Stack, Engine.Blackjack.BjRoundResult Round);

/// <summary>Look and personality of a seat at the table.</summary>
/// <param name="AvatarClass">CSS class for avatars drawn without an emoji (pixel art).</param>
public sealed record SeatLook(string Name, string Avatar, int Hue, string? AvatarClass = null);

public static class Roster
{
    public static SeatLook Human => new(L("Seat.You"), "😎", 45);

    private static readonly SeatLook[] Bots =
    [
        new("Lena", "🦊", 18),
        new("Bruno", "🐻", 30),
        new("Kemal", "🐯", 200),
        new("Mira", "🦉", 275),
        new("Otto", "🐼", 160),
    ];

    public static IReadOnlyList<SeatLook> PickBots(int count, Random random) =>
        Bots.OrderBy(_ => random.Next()).Take(count).ToArray();

    public static string DifficultyName(Difficulty d) => L($"Difficulty.{d}");

    public static string StakeName(TableStake s) => L($"Stake.{s.Id}");
}
