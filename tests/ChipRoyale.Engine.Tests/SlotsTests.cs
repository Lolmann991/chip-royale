using ChipRoyale.Engine.Casino;
using Xunit.Abstractions;

namespace ChipRoyale.Engine.Tests;

public class SlotsTests(ITestOutputHelper output)
{
    private static SlotSymbol[,] Grid(string rows)
    {
        // rows: three lines of five letters, C L G B K(clover) D S(seven) W(wild) X(star)
        var map = new Dictionary<char, SlotSymbol>
        {
            ['C'] = SlotSymbol.Cherry, ['L'] = SlotSymbol.Lemon, ['G'] = SlotSymbol.Grape, ['B'] = SlotSymbol.Bell,
            ['K'] = SlotSymbol.Clover, ['D'] = SlotSymbol.Diamond, ['S'] = SlotSymbol.Seven, ['W'] = SlotSymbol.Wild, ['X'] = SlotSymbol.Star,
        };
        var lines = rows.Split(' ');
        var g = new SlotSymbol[5, 3];
        for (var row = 0; row < 3; row++)
            for (var reel = 0; reel < 5; reel++)
                g[reel, row] = map[lines[row][reel]];
        return g;
    }

    [Fact]
    public void Line_wins_pay_left_to_right_with_wilds()
    {
        // middle line: bell bell wild bell grape → 4 bells = 40 line bets = 4× bet
        var spin = SlotMachine.Evaluate(Grid("CLGKD BBWBG LCGKD"), bet: 10);
        var middle = Assert.Single(spin.Lines, l => l.Line == 0);
        Assert.Equal(SlotSymbol.Bell, middle.Symbol);
        Assert.Equal(4, middle.Count);
        Assert.Equal(40, spin.Win);
    }

    [Fact]
    public void Five_sevens_are_the_jackpot_and_free_spins_double()
    {
        var spin = SlotMachine.Evaluate(Grid("CLGKD SSSSS LCGKD"), bet: 10);
        Assert.True(spin.Jackpot);
        Assert.Equal(1000, spin.Win); // 1000 line bets = 100× bet
        Assert.Equal(2000, SlotMachine.Evaluate(Grid("CLGKD SSSSS LCGKD"), bet: 10, freeSpin: true).Win);
    }

    [Fact]
    public void Stars_pay_anywhere_and_start_free_spins()
    {
        var spin = SlotMachine.Evaluate(Grid("XCLGB LXGBC GLCXD"), bet: 10);
        Assert.Equal(3, spin.Stars);
        Assert.Equal(SlotMachine.FreeSpins, spin.FreeSpinsWon);
        Assert.Equal(20, spin.Win); // 2× bet
        Assert.Equal(3, spin.WinningCells().Count());
    }

    [Fact]
    public void Wild_lines_pay_as_wilds_when_that_is_more()
    {
        var spin = SlotMachine.Evaluate(Grid("CLGKD WWWCG LCGKD"), bet: 10);
        var middle = Assert.Single(spin.Lines, l => l.Line == 0);
        Assert.Equal(SlotSymbol.Wild, middle.Symbol); // 3 wilds (30) beat 4 cherries (13)
        Assert.Equal(30, middle.Multiplier);
    }

    [Fact]
    public void Return_to_player_is_between_93_and_98_percent()
    {
        var machine = new SlotMachine(new Random(7));
        const int bet = 100;
        long paid = 0, won = 0;
        var free = 0;
        var freeTriggers = 0;
        var hits = 0;
        for (var i = 0; i < 400_000; i++)
        {
            var spin = machine.Spin(bet);
            paid += bet;
            won += spin.Win;
            if (spin.Win > 0) hits++;
            if (spin.FreeSpinsWon > 0) freeTriggers++;
            free += spin.FreeSpinsWon;
            while (free > 0)
            {
                free--;
                var f = machine.Spin(bet, freeSpin: true);
                won += f.Win;
                free += f.FreeSpinsWon;
            }
        }
        var rtp = won / (double)paid;
        output.WriteLine($"RTP {rtp:P2}, hit rate {hits / 400_000.0:P1}, free spins every {400_000.0 / freeTriggers:0} spins");
        Assert.InRange(rtp, 0.93, 0.98);
    }
}
