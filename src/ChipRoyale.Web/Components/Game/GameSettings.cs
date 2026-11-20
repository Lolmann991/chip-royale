using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Web.Services;
using static ChipRoyale.Web.Localization.Loc;

namespace ChipRoyale.Web.Components.Game;

/// <param name="BuyIn">Chips the player brings to the table.</param>
public sealed record GameSettings(Difficulty Difficulty, int Opponents, TableStake Stake, int BuyIn)
{
    public int SmallBlind => Stake.SmallBlind;
    public int BigBlind => Stake.BigBlind;
    public bool IsPractice => Stake.IsPractice;
}

/// <param name="Stack">The player's chips at the table right now.</param>
/// <param name="Hand">Set when a hand the player was dealt into has finished.</param>
public readonly record struct TableReport(int Stack, HandOutcome? Hand);

/// <summary>Look and personality of a seat at the table.</summary>
public sealed record SeatLook(string Name, string Avatar, int Hue);

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
