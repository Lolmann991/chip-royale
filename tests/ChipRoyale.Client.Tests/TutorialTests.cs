using ChipRoyale.Engine.Cards;
using ChipRoyale.Client.Components.Tutorial;

namespace ChipRoyale.Client.Tests;

public class TutorialTests
{
    private static LessonStep Step(string lesson, string key) =>
        Lessons.Find(lesson)!.Steps.Single(s => s.Key == key);

    [Theory]
    [InlineData("s3", 0)] // three kings beat two pair
    [InlineData("s4", 0)] // flush beats straight
    [InlineData("s5", 0)] // same pair, king kicker wins
    [InlineData("s6", 2)] // straight on the board: split
    public void Compare_exercises_have_the_answer_the_text_explains(string step, int expected) =>
        Assert.Equal(expected, Step("ranking", step).CompareWinner());

    [Fact]
    public void All_prepared_cards_are_valid_and_unique()
    {
        foreach (var lesson in Lessons.All)
            foreach (var s in lesson.Steps)
            {
                var all = new[] { s.Hole, s.Board, s.HandA, s.HandB }
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .SelectMany(t => Card.ParseMany(t!))
                    .ToList();
                Assert.Equal(all.Count, all.Distinct().Count());
            }
    }

    [Fact]
    public void Quiz_answers_are_within_the_options()
    {
        foreach (var s in Lessons.All.SelectMany(l => l.Steps).Where(s => s.Kind == StepKind.Quiz))
            Assert.InRange(s.Correct, 0, s.Options - 1);
    }

    [Fact]
    public void Seat_roles_follow_the_rules()
    {
        var six = new MiniTableSpec(6, Dealer: 2);
        Assert.Equal(3, six.SeatOf(SeatRole.SmallBlind));
        Assert.Equal(4, six.SeatOf(SeatRole.BigBlind));
        Assert.Equal(5, six.SeatOf(SeatRole.FirstPreflop));
        Assert.Equal(3, six.SeatOf(SeatRole.FirstPostflop));

        var headsUp = new MiniTableSpec(2, Dealer: 0);
        Assert.Equal(0, headsUp.SeatOf(SeatRole.SmallBlind));
        Assert.Equal(1, headsUp.SeatOf(SeatRole.BigBlind));
        Assert.Equal(0, headsUp.SeatOf(SeatRole.FirstPreflop));
        Assert.Equal(1, headsUp.SeatOf(SeatRole.FirstPostflop));
    }

    [Fact]
    public void The_under_the_gun_question_points_at_the_player()
    {
        var s = Step("blinds", "s6");
        Assert.Equal(0, s.Table!.SeatOf(s.Answer)); // the explanation says "that's you"
    }

    [Fact]
    public void Positions_on_a_six_seat_table()
    {
        var t = new MiniTableSpec(6, Dealer: 0);
        Assert.Equal(Position.Late, t.PositionOf(0));
        Assert.Equal(Position.Blinds, t.PositionOf(1));
        Assert.Equal(Position.Blinds, t.PositionOf(2));
        Assert.Equal(Position.Early, t.PositionOf(3));
        Assert.Equal(Position.Middle, t.PositionOf(4));
        Assert.Equal(Position.Late, t.PositionOf(5));
    }
}
