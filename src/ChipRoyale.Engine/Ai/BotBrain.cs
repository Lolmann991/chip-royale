using ChipRoyale.Engine.Holdem;

namespace ChipRoyale.Engine.Ai;

public enum Difficulty
{
    /// <summary>Loose "calling station": pays off good hands, rarely folds, never bluffs big.</summary>
    Easy,
    /// <summary>Plays pot odds and hand strength, bluffs occasionally.</summary>
    Medium,
    /// <summary>Tight-aggressive, bluffs, slow-plays monsters and adapts to an aggressive human.</summary>
    Hard,
}

/// <param name="Looseness">0..1, how willing the bot is to put chips in with weaker hands.</param>
/// <param name="Aggression">0..1, how often it bets/raises instead of checking/calling.</param>
public sealed record BotProfile(Difficulty Difficulty, double Looseness = 0.5, double Aggression = 0.5);

/// <summary>Rule-based poker AI. One instance per bot seat.</summary>
public sealed class BotBrain
{
    private readonly Random _random;

    public BotBrain(BotProfile profile, Random? random = null)
    {
        Profile = profile;
        _random = random ?? Random.Shared;
    }

    public BotProfile Profile { get; }

    /// <summary>Monte-Carlo samples per decision.</summary>
    public int Iterations { get; init; } = 500;

    public PlayerAction Decide(HoldemTable table)
    {
        var idx = table.ActingIndex ?? throw new InvalidOperationException("Nobody has to act.");
        var me = table.Players[idx];
        var legal = table.GetLegalActions();
        var opponents = Math.Max(1, table.ActivePlayerCount - 1);
        var equity = EquityCalculator.Estimate(me.HoleCards, table.Board, Math.Min(opponents, 5), Iterations, _random);

        var s = new Situation(table, me, legal, equity, opponents);
        return Profile.Difficulty switch
        {
            Difficulty.Easy => DecideEasy(s),
            Difficulty.Medium => DecideMedium(s),
            _ => DecideHard(s),
        };
    }

    // ---------------------------------------------------------------- Easy

    private PlayerAction DecideEasy(Situation s)
    {
        var r = _random.NextDouble();
        var loose = Profile.Looseness;

        if (s.Legal.ToCall == 0)
        {
            // Builds pots: bets good hands, and now and then small with mediocre ones.
            if (s.Strength > 0.70 && r < 0.65 + 0.2 * Profile.Aggression) return Bet(s, Between(0.45, 0.75));
            if (s.Strength > 0.55 && r < 0.30 + 0.2 * Profile.Aggression) return Bet(s, 0.33);
            if (r < 0.05) return Bet(s, 0.33);
            return PlayerAction.Check();
        }

        if (s.Strength > 0.80 && r < 0.45 && s.Legal.CanRaise) return Bet(s, Between(0.5, 0.8));

        // Preflop it limps and calls raises with almost anything.
        if (s.Table.Street == Street.Preflop && s.Legal.ToCall <= 6 * s.Table.BigBlind)
            return r < 0.93 ? PlayerAction.Call() : PlayerAction.Fold();

        // Very loose pot-odds rule: happy to pay you off.
        var threshold = s.PotOdds * 0.6 - 0.05 * loose;
        if (s.StackShare > 0.5) threshold = Math.Max(threshold, 0.30 - 0.08 * loose);
        if (s.Equity >= threshold) return PlayerAction.Call();

        // Curious: "let me see what you have".
        if (s.StackShare < 0.25 && r < 0.45 + 0.3 * loose) return PlayerAction.Call();
        if (r < 0.10) return PlayerAction.Call();
        return PlayerAction.Fold();
    }

    // ---------------------------------------------------------------- Medium

    private PlayerAction DecideMedium(Situation s)
    {
        var r = _random.NextDouble();

        if (s.Legal.ToCall == 0)
        {
            if (s.Strength > 0.68 && r < 0.8) return Bet(s, Between(0.5, 0.75));
            if (s.Strength > 0.55 && r < 0.3) return Bet(s, 0.4);
            if (r < 0.07 + 0.05 * Profile.Aggression) return Bet(s, 0.5);
            return PlayerAction.Check();
        }

        if (s.Strength > 0.75 && r < 0.55 && s.Legal.CanRaise) return Bet(s, Between(0.7, 1.0));
        if (s.Table.Street == Street.Preflop && s.Legal.ToCall <= 2 * s.Table.BigBlind && s.Strength > 0.36)
            return PlayerAction.Call();
        if (s.Equity >= s.PotOdds + 0.03) return PlayerAction.Call();
        if (s.StackShare < 0.08 && r < 0.25) return PlayerAction.Call();
        return PlayerAction.Fold();
    }

    // ---------------------------------------------------------------- Hard

    private PlayerAction DecideHard(Situation s)
    {
        var r = _random.NextDouble();
        var humanAggression = HumanAggression(s.Table);

        if (s.Legal.ToCall == 0)
        {
            if (s.Strength > 0.85 && r < 0.3) return PlayerAction.Check(); // slow-play
            if (s.Strength > 0.60 && r < 0.9) return Bet(s, Between(0.65, 1.0));
            var wasAggressor = s.Table.Log.LastOrDefault(e =>
                e.HandNumber == s.Table.HandNumber && e.Action.Type is ActionType.Raise or ActionType.AllIn)?.PlayerIndex
                == s.Table.ActingIndex;
            if (r < (wasAggressor ? 0.45 : 0.18)) return Bet(s, Between(0.5, 0.7)); // c-bet / bluff
            return PlayerAction.Check();
        }

        if (s.Strength > 0.70 && r < 0.7 && s.Legal.CanRaise) return Bet(s, Between(0.9, 1.3));
        if (s.Strength < 0.45 && r < 0.07 && s.Legal.CanRaise && s.Legal.ToCall < s.Legal.Pot * 0.6)
            return Bet(s, 1.0); // bluff raise

        if (s.Table.Street == Street.Preflop && s.Strength < 0.40 && s.Legal.ToCall > s.Table.BigBlind) return PlayerAction.Fold();

        // Assume a bettor's range is stronger than random, unless the human bets everything.
        var betRatio = Math.Min(1.0, s.Legal.ToCall / (double)Math.Max(1, s.Legal.Pot - s.Legal.ToCall));
        var lastAggressorIsHuman = s.Table.Log.LastOrDefault(e => e.HandNumber == s.Table.HandNumber
            && e.Action.Type is ActionType.Raise or ActionType.AllIn) is { } last && s.Table.Players[last.PlayerIndex].IsHuman;
        var adjusted = s.Equity * (1 - 0.12 * betRatio);
        if (lastAggressorIsHuman && humanAggression > 0.3) adjusted += 0.08;

        if (adjusted >= s.PotOdds + 0.02) return PlayerAction.Call();
        return PlayerAction.Fold();
    }

    // ---------------------------------------------------------------- helpers

    private static double HumanAggression(HoldemTable table)
    {
        var human = table.Players.Select((p, i) => (p, i)).FirstOrDefault(x => x.p.IsHuman);
        if (human.p is null) return 0;
        var actions = table.Log.Where(e => e.PlayerIndex == human.i).TakeLast(40).ToList();
        if (actions.Count < 6) return 0;
        return actions.Count(e => e.Action.Type is ActionType.Raise or ActionType.AllIn) / (double)actions.Count;
    }

    private double Between(double min, double max) => min + _random.NextDouble() * (max - min);

    /// <summary>Bet or raise by a fraction of the pot (after calling).</summary>
    private static PlayerAction Bet(Situation s, double potFraction)
    {
        var legal = s.Legal;
        if (!legal.CanRaise)
            return legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call();

        var potAfterCall = legal.Pot + legal.ToCall;
        var raiseTo = legal.CurrentBet + (int)(potAfterCall * potFraction);
        var unit = Math.Max(1, s.Table.SmallBlind);
        raiseTo = (int)Math.Round(raiseTo / (double)unit) * unit;
        raiseTo = Math.Max(raiseTo, legal.MinRaiseTo);

        // Don't leave a silly little stack behind.
        if (raiseTo >= legal.MaxRaiseTo * 0.8) return PlayerAction.AllIn();
        return PlayerAction.RaiseTo(raiseTo);
    }

    private sealed class Situation
    {
        public Situation(HoldemTable table, Player me, LegalActions legal, double equity, int opponents)
        {
            Table = table;
            Legal = legal;
            Equity = equity;
            // Normalise multi-way equity to a heads-up-like strength scale (0..1).
            Strength = Math.Pow(Math.Max(equity, 1e-6), 1.0 / opponents);
            PotOdds = legal.ToCall == 0 ? 0 : legal.ToCall / (double)(legal.Pot + legal.ToCall);
            StackShare = legal.ToCall / (double)Math.Max(1, me.Chips + me.StreetBet);
        }

        public HoldemTable Table { get; }
        public LegalActions Legal { get; }
        public double Equity { get; }
        public double Strength { get; }
        public double PotOdds { get; }
        public double StackShare { get; }
    }
}
