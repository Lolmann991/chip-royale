using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Holdem;

/// <summary>
/// No-Limit Texas Hold'em hand flow: blinds, betting rounds, side pots and showdown.
/// The table is driven step by step so a UI can animate every action:
/// <list type="number">
/// <item><see cref="StartHand"/></item>
/// <item>while <see cref="Phase"/> is <see cref="TablePhase.WaitingForAction"/>: <see cref="Act"/> for <see cref="ActingIndex"/></item>
/// <item>when <see cref="TablePhase.RoundComplete"/>: <see cref="Advance"/> (next street or showdown)</item>
/// <item>until <see cref="TablePhase.HandComplete"/>; read <see cref="LastResult"/></item>
/// </list>
/// </summary>
public sealed class HoldemTable
{
    private readonly List<Player> _players;
    private readonly List<Card> _board = new(5);
    private readonly List<ActionLogEntry> _log = new();
    private readonly Random _random;
    private readonly Deck _deck;

    public HoldemTable(IEnumerable<Player> players, int smallBlind, int bigBlind, Random? random = null)
    {
        _players = players.ToList();
        if (_players.Count is < 2 or > 10)
            throw new ArgumentException("A table needs 2 to 10 players.", nameof(players));
        if (smallBlind <= 0 || bigBlind < smallBlind)
            throw new ArgumentException("Invalid blinds.");

        SmallBlind = smallBlind;
        BigBlind = bigBlind;
        _random = random ?? Random.Shared;
        _deck = new Deck(_random);
        DealerIndex = _random.Next(_players.Count);
    }

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<Card> Board => _board;
    public IReadOnlyList<ActionLogEntry> Log => _log;

    public int SmallBlind { get; }
    public int BigBlind { get; }

    public int HandNumber { get; private set; }
    public TablePhase Phase { get; private set; } = TablePhase.Idle;
    public Street Street { get; private set; }
    public int DealerIndex { get; private set; }
    public int SmallBlindIndex { get; private set; } = -1;
    public int BigBlindIndex { get; private set; } = -1;
    public int? ActingIndex { get; private set; }

    /// <summary>Highest street total anyone has to match.</summary>
    public int CurrentBet { get; private set; }

    /// <summary>Size of the last full raise (minimum raise increment).</summary>
    public int MinRaise { get; private set; }

    /// <summary>All chips in the middle, including bets of the current round.</summary>
    public int Pot => _players.Sum(p => p.TotalInvested);

    /// <summary>Chips already collected from earlier rounds (without the current street's bets).</summary>
    public int CollectedPot => _players.Sum(p => p.TotalInvested - p.StreetBet);

    public HandResult? LastResult { get; private set; }

    public bool CanStartHand => _players.Count(p => p.Chips > 0) >= 2;

    /// <summary>All remaining players are all-in (or only one can still act): the board is run out without betting.</summary>
    public bool IsRunout =>
        Phase == TablePhase.RoundComplete && _players.Count(p => p.IsActive) >= 2 && _players.Count(p => p.CanAct) <= 1;

    public int ActivePlayerCount => _players.Count(p => p.IsActive);

    public void StartHand()
    {
        if (Phase is TablePhase.WaitingForAction or TablePhase.RoundComplete)
            throw new InvalidOperationException("The current hand is not finished yet.");
        if (!CanStartHand)
            throw new InvalidOperationException("Need at least two players with chips.");

        HandNumber++;
        LastResult = null;
        _board.Clear();
        _deck.Reset();
        _deck.Shuffle();

        foreach (var p in _players)
        {
            p.Hole.Clear();
            p.StreetBet = 0;
            p.TotalInvested = 0;
            p.HasFolded = false;
            p.HasActed = false;
            p.LastAction = null;
            p.InHand = p.Chips > 0;
        }

        DealerIndex = NextIndex(HandNumber == 1 ? DealerIndex - 1 : DealerIndex, p => p.InHand);
        var inHand = _players.Count(p => p.InHand);
        if (inHand == 2)
        {
            SmallBlindIndex = DealerIndex;
            BigBlindIndex = NextIndex(DealerIndex, p => p.InHand);
        }
        else
        {
            SmallBlindIndex = NextIndex(DealerIndex, p => p.InHand);
            BigBlindIndex = NextIndex(SmallBlindIndex, p => p.InHand);
        }

        Street = Street.Preflop;
        Post(SmallBlindIndex, SmallBlind);
        Post(BigBlindIndex, BigBlind);
        CurrentBet = BigBlind;
        MinRaise = BigBlind;

        // Deal two cards each, one at a time, starting left of the dealer.
        for (var round = 0; round < 2; round++)
        {
            var i = DealerIndex;
            for (var n = 0; n < inHand; n++)
            {
                i = NextIndex(i, p => p.InHand);
                _players[i].Hole.Add(_deck.Draw());
            }
        }

        Phase = TablePhase.WaitingForAction;
        ActingIndex = BigBlindIndex;
        MoveToNextActorOrEndRound();
    }

    public LegalActions GetLegalActions()
    {
        if (Phase != TablePhase.WaitingForAction || ActingIndex is not { } idx)
            throw new InvalidOperationException("Nobody has to act right now.");

        var p = _players[idx];
        var toCall = Math.Min(CurrentBet - p.StreetBet, p.Chips);
        var maxRaiseTo = p.StreetBet + p.Chips;
        var othersCanAct = _players.Where((o, i) => i != idx && o.CanAct).Any();
        var canRaise = maxRaiseTo > CurrentBet && othersCanAct;
        var minRaiseTo = Math.Min(CurrentBet + MinRaise, maxRaiseTo);
        return new LegalActions(
            CanCheck: p.StreetBet >= CurrentBet,
            ToCall: Math.Max(0, toCall),
            CanRaise: canRaise,
            MinRaiseTo: minRaiseTo,
            MaxRaiseTo: maxRaiseTo,
            CurrentBet: CurrentBet,
            StreetBet: p.StreetBet,
            Pot: Pot);
    }

    /// <summary>
    /// Applies an action for the acting player. Invalid sizes are clamped to the nearest legal
    /// action (e.g. check when nothing to call becomes a check, a too-small raise becomes a min raise).
    /// </summary>
    public PlayerAction Act(PlayerAction action)
    {
        if (Phase != TablePhase.WaitingForAction || ActingIndex is not { } idx)
            throw new InvalidOperationException("Nobody has to act right now.");

        var p = _players[idx];
        var legal = GetLegalActions();
        var applied = Normalize(action, legal);

        switch (applied.Type)
        {
            case ActionType.Fold:
                p.HasFolded = true;
                break;
            case ActionType.Check:
                break;
            case ActionType.Call:
                PutIn(p, legal.ToCall);
                applied = applied with { Amount = legal.ToCall };
                break;
            case ActionType.Raise:
            case ActionType.AllIn:
            {
                var raiseTo = applied.Type == ActionType.AllIn ? legal.MaxRaiseTo : applied.Amount;
                PutIn(p, raiseTo - p.StreetBet);
                if (p.StreetBet > CurrentBet)
                {
                    var increment = p.StreetBet - CurrentBet;
                    if (increment >= MinRaise) MinRaise = increment;
                    CurrentBet = p.StreetBet;
                    foreach (var other in _players)
                        if (other != p && other.CanAct) other.HasActed = false;
                }
                applied = applied with { Amount = p.StreetBet };
                break;
            }
        }

        p.HasActed = true;
        p.LastAction = applied;
        _log.Add(new ActionLogEntry(HandNumber, Street, idx, applied));

        if (ActivePlayerCount == 1)
        {
            ReturnUncalledBet();
            AwardToLastPlayer();
            return applied;
        }

        MoveToNextActorOrEndRound();
        return applied;
    }

    /// <summary>Deals the next street (or shows down) after a completed betting round.</summary>
    public void Advance()
    {
        if (Phase != TablePhase.RoundComplete)
            throw new InvalidOperationException("The betting round is not complete.");

        foreach (var p in _players)
        {
            p.StreetBet = 0;
            p.HasActed = false;
        }
        CurrentBet = 0;
        MinRaise = BigBlind;

        if (Street == Street.River)
        {
            Showdown();
            return;
        }

        Street++;
        _deck.Draw(); // burn
        var count = Street == Street.Flop ? 3 : 1;
        _board.AddRange(_deck.Draw(count));

        foreach (var p in _players)
            if (p.CanAct) p.LastAction = null;

        if (_players.Count(p => p.CanAct) >= 2)
        {
            Phase = TablePhase.WaitingForAction;
            ActingIndex = DealerIndex;
            MoveToNextActorOrEndRound();
        }
        else
        {
            Phase = TablePhase.RoundComplete;
            ActingIndex = null;
        }
    }

    private PlayerAction Normalize(PlayerAction action, LegalActions legal)
    {
        switch (action.Type)
        {
            case ActionType.Fold:
                return action;
            case ActionType.Check:
            case ActionType.Call:
                return legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call();
            case ActionType.AllIn:
                if (legal.MaxRaiseTo <= legal.CurrentBet || !legal.CanRaise)
                    return legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call();
                return PlayerAction.AllIn();
            case ActionType.Raise:
                if (!legal.CanRaise)
                    return legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call();
                var to = Math.Clamp(action.Amount, legal.MinRaiseTo, legal.MaxRaiseTo);
                return to >= legal.MaxRaiseTo ? PlayerAction.AllIn() : PlayerAction.RaiseTo(to);
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    private void MoveToNextActorOrEndRound()
    {
        var canAct = _players.Count(p => p.CanAct);
        bool NeedsToAct(Player p) =>
            p.CanAct && (p.StreetBet < CurrentBet || (!p.HasActed && canAct >= 2));

        if (!_players.Any(NeedsToAct))
        {
            ReturnUncalledBet();
            Phase = TablePhase.RoundComplete;
            ActingIndex = null;
            return;
        }

        ActingIndex = NextIndex(ActingIndex ?? DealerIndex, NeedsToAct);
    }

    private void Post(int index, int amount)
    {
        var p = _players[index];
        PutIn(p, Math.Min(amount, p.Chips));
    }

    private static void PutIn(Player p, int amount)
    {
        amount = Math.Min(amount, p.Chips);
        p.Chips -= amount;
        p.StreetBet += amount;
        p.TotalInvested += amount;
    }

    /// <summary>A bet nobody matched goes back to its owner.</summary>
    private void ReturnUncalledBet()
    {
        var inHand = _players.Where(p => p.InHand).OrderByDescending(p => p.StreetBet).ToList();
        if (inHand.Count < 2) return;
        var top = inHand[0];
        var excess = top.StreetBet - inHand[1].StreetBet;
        if (excess <= 0) return;
        top.StreetBet -= excess;
        top.TotalInvested -= excess;
        top.Chips += excess;
    }

    private void AwardToLastPlayer()
    {
        var winnerIndex = _players.FindIndex(p => p.IsActive);
        var pot = Pot;
        _players[winnerIndex].Chips += pot;
        var allIn = _players.Select((_, i) => i).Where(i => _players[i].InHand).ToArray();
        LastResult = new HandResult(
            wasShowdown: false,
            pots: [new PotResult(pot, [winnerIndex], allIn)],
            hands: new Dictionary<int, HandValue>(),
            winnings: new Dictionary<int, int> { [winnerIndex] = pot });
        FinishHand();
    }

    private void Showdown()
    {
        Street = Street.Showdown;
        var contenders = _players.Select((p, i) => (p, i)).Where(x => x.p.IsActive).ToList();
        var hands = contenders.ToDictionary(
            x => x.i,
            x => HandEvaluator.EvaluateBest([.. x.p.HoleCards, .. _board]));

        var winnings = new Dictionary<int, int>();
        var pots = new List<PotResult>();
        var levels = contenders.Select(x => x.p.TotalInvested).Distinct().Order().ToList();
        var previous = 0;
        var distributed = 0;

        foreach (var level in levels)
        {
            var amount = _players.Sum(p => Math.Clamp(p.TotalInvested - previous, 0, level - previous));
            if (level == levels[^1])
                amount = Pot - distributed; // leftover from folded players' bigger contributions
            var eligible = contenders.Where(x => x.p.TotalInvested >= level).Select(x => x.i).ToList();
            previous = level;
            if (amount <= 0) continue;

            var best = eligible.Select(i => hands[i]).Max()!;
            var winners = OrderFromDealer(eligible.Where(i => hands[i].CompareTo(best) == 0)).ToList();
            var share = amount / winners.Count;
            var odd = amount - share * winners.Count;
            for (var w = 0; w < winners.Count; w++)
            {
                var won = share + (w < odd ? 1 : 0);
                winnings[winners[w]] = winnings.GetValueOrDefault(winners[w]) + won;
            }
            pots.Add(new PotResult(amount, winners, eligible));
            distributed += amount;
        }

        foreach (var (i, won) in winnings)
            _players[i].Chips += won;

        LastResult = new HandResult(true, pots, hands, winnings);
        FinishHand();
    }

    private void FinishHand()
    {
        foreach (var p in _players)
        {
            p.StreetBet = 0;
            p.IsEliminated = p.Chips == 0;
        }
        CurrentBet = 0;
        ActingIndex = null;
        Phase = TablePhase.HandComplete;
    }

    private IEnumerable<int> OrderFromDealer(IEnumerable<int> indices)
    {
        var n = _players.Count;
        return indices.OrderBy(i => (i - DealerIndex - 1 + n) % n);
    }

    private int NextIndex(int from, Func<Player, bool> predicate)
    {
        var n = _players.Count;
        for (var step = 1; step <= n; step++)
        {
            var i = ((from + step) % n + n) % n;
            if (predicate(_players[i])) return i;
        }
        throw new InvalidOperationException("No matching player.");
    }
}
