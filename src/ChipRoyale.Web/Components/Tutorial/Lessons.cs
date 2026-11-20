using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Web.Components.Tutorial;

public enum StepKind
{
    /// <summary>Explanation with optional cards, mini table or hand ranking list.</summary>
    Info,
    /// <summary>Multiple choice; options are <c>Learn.{lesson}.{key}.0..n</c>.</summary>
    Quiz,
    /// <summary>"Who wins?" between hand A and hand B on a board; the answer is computed by the engine.</summary>
    Compare,
    /// <summary>Click the right seat on the mini table.</summary>
    Seat,
}

/// <summary>Seat roles on the mini table, computed from the dealer position.</summary>
public enum SeatRole { Button, SmallBlind, BigBlind, FirstPreflop, FirstPostflop }

public enum Position { Blinds, Early, Middle, Late }

/// <param name="Order">Show acting order numbers: "preflop" or "postflop".</param>
public sealed record MiniTableSpec(int Seats, int Dealer, bool ShowBlinds = true, bool ShowPositions = false, string? Order = null)
{
    public bool HeadsUp => Seats == 2;

    public int SeatOf(SeatRole role) => role switch
    {
        SeatRole.Button => Dealer,
        SeatRole.SmallBlind => HeadsUp ? Dealer : Next(Dealer, 1),
        SeatRole.BigBlind => HeadsUp ? Next(Dealer, 1) : Next(Dealer, 2),
        SeatRole.FirstPreflop => HeadsUp ? Dealer : Next(Dealer, 3),
        _ => Next(Dealer, 1),
    };

    public Position PositionOf(int seat)
    {
        var r = (seat - Dealer + Seats) % Seats;
        if (r == 0) return Position.Late;
        if (r is 1 or 2) return Position.Blinds;
        var rest = Seats - 3; // seats between the big blind and the button
        var idx = r - 3;
        return idx < rest / 3.0 ? Position.Early : idx < rest * 2 / 3.0 ? Position.Middle : Position.Late;
    }

    /// <summary>1-based acting order of a seat, or null when no order is shown.</summary>
    public int? OrderOf(int seat)
    {
        if (Order is null) return null;
        var first = SeatOf(Order == "preflop" ? SeatRole.FirstPreflop : SeatRole.FirstPostflop);
        return (seat - first + Seats) % Seats + 1;
    }

    private int Next(int seat, int steps) => (seat + steps) % Seats;
}

public sealed record LessonStep(StepKind Kind, string Key)
{
    public string? Hole { get; init; }
    public string? Board { get; init; }

    /// <summary>Highlight the best five cards of hole + board and name the hand.</summary>
    public bool ShowBest { get; init; }

    public bool ShowRankings { get; init; }
    public MiniTableSpec? Table { get; init; }

    /// <summary>Quiz: number of options and the index of the right one.</summary>
    public int Options { get; init; }
    public int Correct { get; init; }

    /// <summary>Seat quiz: the role the player has to click.</summary>
    public SeatRole Answer { get; init; }

    /// <summary>Compare: the two hands (two cards each).</summary>
    public string? HandA { get; init; }
    public string? HandB { get; init; }

    /// <summary>Text keys may add an explanation shown after answering (<c>{key}.Explain</c>).</summary>
    public bool HasExplain { get; init; } = true;

    /// <summary>0 = hand A wins, 1 = hand B wins, 2 = split pot.</summary>
    public int CompareWinner()
    {
        var board = Card.ParseMany(Board!);
        var a = HandEvaluator.EvaluateBest([.. Card.ParseMany(HandA!), .. board]);
        var b = HandEvaluator.EvaluateBest([.. Card.ParseMany(HandB!), .. board]);
        var c = a.CompareTo(b);
        return c > 0 ? 0 : c < 0 ? 1 : 2;
    }
}

public sealed record Lesson(string Id, string Icon, IReadOnlyList<LessonStep> Steps);

/// <summary>The guided lessons. Texts live in the resources under <c>Learn.{lessonId}.*</c>.</summary>
public static class Lessons
{
    public static readonly IReadOnlyList<Lesson> All =
    [
        new("ranking", "🃏",
        [
            new(StepKind.Info, "s1") { Hole = "Ah Kd", Board = "Ac 7s 7d 2h Ks", ShowBest = true },
            new(StepKind.Info, "s2") { ShowRankings = true },
            new(StepKind.Compare, "s3") { HandA = "Ks Kh", HandB = "As 9d", Board = "Kd 7c 2s 9h 4c", HasExplain = false },
            new(StepKind.Compare, "s4") { HandA = "Ah 3h", HandB = "Tc 6d", Board = "9h 8h 7c 2h Js", HasExplain = false },
            new(StepKind.Compare, "s5") { HandA = "Ad Kc", HandB = "Ah Qs", Board = "Ac 9d 5s 3h 2c" },
            new(StepKind.Compare, "s6") { HandA = "2c 3d", HandB = "4h 5h", Board = "Ts Js Qd Kh Ac" },
            new(StepKind.Quiz, "s7") { Options = 3, Correct = 0 },
        ]),
        new("rounds", "🔄",
        [
            new(StepKind.Info, "s1") { Hole = "Qs Jd", Board = "" },
            new(StepKind.Info, "s2") { Hole = "Qs Jd", Board = "Ts 9c 3h", ShowBest = true },
            new(StepKind.Info, "s3") { Hole = "Qs Jd", Board = "Ts 9c 3h 8d", ShowBest = true },
            new(StepKind.Info, "s4") { Hole = "Qs Jd", Board = "Ts 9c 3h 8d 2c", ShowBest = true },
            new(StepKind.Info, "s5"),
            new(StepKind.Quiz, "s6") { Options = 3, Correct = 2 },
            new(StepKind.Quiz, "s7") { Options = 3, Correct = 0 },
            new(StepKind.Quiz, "s8") { Options = 3, Correct = 2 },
        ]),
        new("blinds", "💰",
        [
            new(StepKind.Info, "s1") { Table = new(6, 0) },
            new(StepKind.Info, "s2") { Table = new(6, 1) },
            new(StepKind.Seat, "s3") { Table = new(6, 2, ShowBlinds: false), Answer = SeatRole.BigBlind },
            new(StepKind.Quiz, "s4") { Options = 3, Correct = 1 },
            new(StepKind.Info, "s5") { Table = new(6, 0, Order: "preflop") },
            new(StepKind.Seat, "s6") { Table = new(6, 3), Answer = SeatRole.FirstPreflop },
            new(StepKind.Info, "s7") { Table = new(2, 0) },
        ]),
        new("positions", "📍",
        [
            new(StepKind.Info, "s1") { Table = new(6, 0, Order: "postflop") },
            new(StepKind.Info, "s2") { Table = new(6, 0, ShowPositions: true) },
            new(StepKind.Seat, "s3") { Table = new(6, 4, ShowBlinds: false), Answer = SeatRole.Button },
            new(StepKind.Seat, "s4") { Table = new(6, 4, ShowBlinds: false), Answer = SeatRole.FirstPostflop },
            new(StepKind.Quiz, "s5") { Hole = "7c 2d", Options = 3, Correct = 0 },
            new(StepKind.Quiz, "s6") { Hole = "Ah Kh", Options = 3, Correct = 0 },
            new(StepKind.Info, "s7"),
        ]),
    ];

    public static Lesson? Find(string? id) => All.FirstOrDefault(l => l.Id == id);
}
