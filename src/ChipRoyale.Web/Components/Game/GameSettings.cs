using ChipRoyale.Engine.Ai;

namespace ChipRoyale.Web.Components.Game;

public sealed record GameSettings(Difficulty Difficulty, int Opponents, int StartChips)
{
    public int SmallBlind => Math.Max(1, StartChips / 100);
    public int BigBlind => SmallBlind * 2;
}

/// <summary>Look and personality of a seat at the table.</summary>
public sealed record SeatLook(string Name, string Avatar, int Hue);

public static class Roster
{
    public static readonly SeatLook Human = new("Du", "😎", 45);

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

    public static string DifficultyName(Difficulty d) => d switch
    {
        Difficulty.Easy => "Leicht",
        Difficulty.Medium => "Mittel",
        _ => "Schwer",
    };
}
