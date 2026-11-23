using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Blackjack;

public enum BjPhase
{
    /// <summary>Waiting for the bet.</summary>
    Betting,
    PlayerTurn,
    DealerTurn,
    Finished,
}

public enum BjOutcome
{
    Pending,
    /// <summary>Natural 21 with the first two cards: pays 3:2.</summary>
    Blackjack,
    Win,
    Push,
    Lose,
    Bust,
}

public enum BjAction
{
    Hit,
    Stand,
    Double,
    Split,
}

/// <summary>Table rules (the common casino set: 6 decks, dealer stands on soft 17, 3:2, double after split).</summary>
public sealed record BlackjackRules
{
    public int Decks { get; init; } = 6;
    public bool DealerHitsSoft17 { get; init; }
    public bool DoubleAfterSplit { get; init; } = true;

    /// <summary>Most hands after splitting (3 splits).</summary>
    public int MaxHands { get; init; } = 4;

    /// <summary>The shoe is reshuffled after this share of cards (cut card).</summary>
    public double Penetration { get; init; } = 0.75;

    public static readonly BlackjackRules Standard = new();
}

public static class BjValue
{
    /// <summary>Ace 11 (or 1), face cards 10.</summary>
    public static int Of(Rank rank) => rank == Rank.Ace ? 11 : Math.Min(10, (int)rank);

    /// <summary>Best total and whether an ace still counts 11 (soft hand).</summary>
    public static (int Total, bool Soft) Of(IEnumerable<Card> cards)
    {
        int total = 0, aces = 0;
        foreach (var c in cards)
        {
            total += Of(c.Rank);
            if (c.Rank == Rank.Ace) aces++;
        }
        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }
        return (total, aces > 0);
    }

    /// <summary>Hi-Lo card counting: 2–6 = +1, 7–9 = 0, 10–A = −1.</summary>
    public static int HiLo(Card c) => Of(c.Rank) switch
    {
        <= 6 => 1,
        <= 9 => 0,
        _ => -1,
    };
}

/// <summary>Several decks shuffled together; reshuffled at the cut card.</summary>
public sealed class Shoe
{
    private readonly List<Card> _cards = [];
    private readonly Random _random;
    private readonly double _penetration;
    private int _position;

    public Shoe(int decks = 6, Random? random = null, double penetration = 0.75)
    {
        _random = random ?? Random.Shared;
        _penetration = penetration;
        for (var d = 0; d < decks; d++)
            foreach (var suit in Enum.GetValues<Suit>())
                foreach (var rank in Enum.GetValues<Rank>())
                    _cards.Add(new Card(rank, suit));
        Shuffle();
    }

    /// <summary>A shoe that deals exactly these cards first (tests, tutorials); afterwards a normal shuffle.</summary>
    public static Shoe Stacked(IEnumerable<Card> first, int decks = 6)
    {
        var shoe = new Shoe(decks, new Random(1));
        var order = first.ToList();
        foreach (var c in order) shoe._cards.Remove(c);
        shoe._cards.InsertRange(0, order);
        return shoe;
    }

    public int Count => _cards.Count;
    public int Remaining => _cards.Count - _position;
    public bool NeedsShuffle => _position >= _cards.Count * _penetration;

    /// <summary>Hi-Lo running count of the cards dealt since the last shuffle.</summary>
    public int RunningCount { get; private set; }

    /// <summary>Running count per remaining deck.</summary>
    public double TrueCount => RunningCount / Math.Max(0.5, Remaining / 52.0);

    public void Shuffle()
    {
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
        _position = 0;
        RunningCount = 0;
    }

    /// <param name="counted">False for the dealer's hole card (counted when it is turned).</param>
    public Card Draw(bool counted = true)
    {
        if (_position >= _cards.Count) Shuffle();
        var card = _cards[_position++];
        if (counted) RunningCount += BjValue.HiLo(card);
        return card;
    }

    /// <summary>Counts a card dealt face down earlier (the dealer's hole card when it is turned).</summary>
    public void CountCard(Card card) => RunningCount += BjValue.HiLo(card);

    /// <summary>How many cards of each value (2–11) have not been dealt yet.</summary>
    public int[] RemainingByValue()
    {
        var counts = new int[12];
        for (var i = _position; i < _cards.Count; i++) counts[BjValue.Of(_cards[i].Rank)]++;
        return counts;
    }
}

public sealed class BjHand
{
    public List<Card> Cards { get; } = [];
    public int Bet { get; internal set; }
    public bool Doubled { get; internal set; }

    /// <summary>Created by splitting (a 21 then is no blackjack).</summary>
    public bool FromSplit { get; internal set; }

    /// <summary>Split aces get exactly one card each.</summary>
    public bool SplitAces { get; internal set; }

    public bool Done { get; internal set; }
    public BjOutcome Outcome { get; internal set; }

    /// <summary>Chips paid back for this hand (bet included); 0 when lost.</summary>
    public int Payout { get; internal set; }

    public int Total => BjValue.Of(Cards).Total;
    public bool IsSoft => BjValue.Of(Cards).Soft;
    public bool IsBlackjack => Cards.Count == 2 && Total == 21 && !FromSplit;
    public bool IsBust => Total > 21;
    public int Net => Payout - Bet;
}

/// <summary>Summary of a finished round (for statistics and achievements).</summary>
public sealed record BjRoundResult(int Bet, int Payout, bool Natural, bool DoubleWon, bool SplitWon)
{
    public int Net => Payout - Bet;
    public bool Won => Net > 0;
}

/// <summary>
/// One player against the dealer. Chips at the table are <see cref="Stack"/>; bets leave the stack
/// when placed and winnings come back when the round is settled.
/// </summary>
public sealed class BlackjackRound(Shoe shoe, BlackjackRules rules, int stack)
{
    private readonly List<BjHand> _hands = [];
    private readonly List<Card> _dealer = [];

    public Shoe Shoe { get; } = shoe;
    public BlackjackRules Rules { get; } = rules;
    public int Stack { get; private set; } = stack;
    public BjPhase Phase { get; private set; } = BjPhase.Betting;

    public IReadOnlyList<BjHand> Hands => _hands;
    public int ActiveIndex { get; private set; }
    public BjHand? Active => Phase == BjPhase.PlayerTurn && ActiveIndex < _hands.Count ? _hands[ActiveIndex] : null;

    public IReadOnlyList<Card> Dealer => _dealer;
    public bool HoleRevealed { get; private set; }
    public Card? DealerUp => _dealer.Count > 0 ? _dealer[0] : null;
    public int DealerTotal => BjValue.Of(HoleRevealed ? _dealer : _dealer.Take(1)).Total;
    public bool DealerSoft => BjValue.Of(HoleRevealed ? _dealer : _dealer.Take(1)).Soft;

    /// <summary>The shoe was reshuffled before this round.</summary>
    public bool Shuffled { get; private set; }

    public BjRoundResult? Result { get; private set; }

    public int TotalBet => _hands.Sum(h => h.Bet);

    // ------------------------------------------------------------------ betting

    public void Deal(int bet)
    {
        if (Phase != BjPhase.Betting) throw new InvalidOperationException("A round is already running.");
        if (bet <= 0 || bet > Stack) throw new ArgumentOutOfRangeException(nameof(bet));
        Stack -= bet;
        var hand = new BjHand { Bet = bet };
        _hands.Add(hand);

        hand.Cards.Add(Shoe.Draw());
        _dealer.Add(Shoe.Draw());
        hand.Cards.Add(Shoe.Draw());
        _dealer.Add(Shoe.Draw(counted: false));
        Phase = BjPhase.PlayerTurn;
        ActiveIndex = 0;

        // Dealer peeks under an ace or a ten: a dealer blackjack ends the round at once.
        var dealerBlackjack = BjValue.Of(_dealer).Total == 21;
        if (dealerBlackjack || hand.IsBlackjack)
        {
            Reveal();
            Settle();
        }
    }

    /// <summary>Clears the table for the next bet (reshuffles at the cut card).</summary>
    public void NextRound()
    {
        if (Phase != BjPhase.Finished) throw new InvalidOperationException("The round is not finished.");
        _hands.Clear();
        _dealer.Clear();
        HoleRevealed = false;
        Result = null;
        Phase = BjPhase.Betting;
        Shuffled = Shoe.NeedsShuffle;
        if (Shuffled) Shoe.Shuffle();
    }

    // ------------------------------------------------------------------ player actions

    public bool CanHit => Active is { SplitAces: false } h && h.Total < 21;

    public bool CanDouble => Active is { } h && h.Cards.Count == 2 && !h.SplitAces && h.Total < 21
        && (!h.FromSplit || Rules.DoubleAfterSplit) && Stack >= h.Bet;

    public bool CanSplit => Active is { } h && h.Cards.Count == 2 && !h.SplitAces
        && BjValue.Of(h.Cards[0].Rank) == BjValue.Of(h.Cards[1].Rank)
        && _hands.Count < Rules.MaxHands && Stack >= h.Bet;

    public bool Can(BjAction action) => action switch
    {
        BjAction.Hit => CanHit,
        BjAction.Stand => Active is not null,
        BjAction.Double => CanDouble,
        BjAction.Split => CanSplit,
        _ => false,
    };

    public void Act(BjAction action)
    {
        if (!Can(action)) throw new InvalidOperationException($"{action} is not possible now.");
        var hand = Active!;
        switch (action)
        {
            case BjAction.Hit:
                hand.Cards.Add(Shoe.Draw());
                if (hand.Total >= 21) Advance();
                break;
            case BjAction.Stand:
                Advance();
                break;
            case BjAction.Double:
                Stack -= hand.Bet;
                hand.Bet *= 2;
                hand.Doubled = true;
                hand.Cards.Add(Shoe.Draw());
                Advance();
                break;
            case BjAction.Split:
                Stack -= hand.Bet;
                var aces = hand.Cards[0].Rank == Rank.Ace;
                var second = new BjHand { Bet = hand.Bet, FromSplit = true, SplitAces = aces };
                second.Cards.Add(hand.Cards[1]);
                hand.Cards.RemoveAt(1);
                hand.FromSplit = true;
                hand.SplitAces = aces;
                _hands.Insert(ActiveIndex + 1, second);
                hand.Cards.Add(Shoe.Draw());
                if (aces)
                {
                    second.Cards.Add(Shoe.Draw());
                    hand.Done = second.Done = true;
                    ActiveIndex++;
                    Advance();
                }
                else if (hand.Total >= 21)
                {
                    Advance();
                }
                break;
        }
    }

    /// <summary>Finishes the active hand and moves to the next one (or to the dealer).</summary>
    private void Advance()
    {
        _hands[ActiveIndex].Done = true;
        ActiveIndex++;
        while (ActiveIndex < _hands.Count)
        {
            var next = _hands[ActiveIndex];
            if (next.Cards.Count == 1) next.Cards.Add(Shoe.Draw());
            if (next.Done || next.Total >= 21)
            {
                next.Done = true;
                ActiveIndex++;
                continue;
            }
            return;
        }

        Phase = BjPhase.DealerTurn;
        Reveal();
        // Nothing left to beat: the dealer does not draw.
        if (_hands.All(h => h.IsBust)) Settle();
    }

    // ------------------------------------------------------------------ dealer

    public bool DealerMustDraw =>
        Phase == BjPhase.DealerTurn && (DealerTotal < 17 || (DealerTotal == 17 && DealerSoft && Rules.DealerHitsSoft17));

    /// <summary>The dealer takes one card (call while <see cref="DealerMustDraw"/>; one at a time for the animation).</summary>
    public Card DealerDraw()
    {
        if (!DealerMustDraw) throw new InvalidOperationException("The dealer stands.");
        var card = Shoe.Draw();
        _dealer.Add(card);
        return card;
    }

    /// <summary>Ends the dealer's turn and pays the hands.</summary>
    public void DealerStands()
    {
        if (Phase != BjPhase.DealerTurn || DealerMustDraw) throw new InvalidOperationException("The dealer has to draw.");
        Settle();
    }

    /// <summary>Plays the dealer's hand to the end at once.</summary>
    public void PlayDealer()
    {
        while (DealerMustDraw) DealerDraw();
        if (Phase == BjPhase.DealerTurn) Settle();
    }

    private void Reveal()
    {
        if (HoleRevealed) return;
        HoleRevealed = true;
        Shoe.CountCard(_dealer[1]);
    }

    private void Settle()
    {
        var dealer = BjValue.Of(_dealer).Total;
        var dealerBlackjack = _dealer.Count == 2 && dealer == 21;
        foreach (var h in _hands)
        {
            (h.Outcome, h.Payout) = h switch
            {
                { IsBust: true } => (BjOutcome.Bust, 0),
                { IsBlackjack: true } when dealerBlackjack => (BjOutcome.Push, h.Bet),
                { IsBlackjack: true } => (BjOutcome.Blackjack, h.Bet + h.Bet * 3 / 2),
                _ when dealerBlackjack => (BjOutcome.Lose, 0),
                _ when dealer > 21 || h.Total > dealer => (BjOutcome.Win, h.Bet * 2),
                _ when h.Total == dealer => (BjOutcome.Push, h.Bet),
                _ => (BjOutcome.Lose, 0),
            };
            h.Done = true;
            Stack += h.Payout;
        }
        Phase = BjPhase.Finished;
        var split = _hands.Count > 1;
        Result = new BjRoundResult(
            TotalBet,
            _hands.Sum(h => h.Payout),
            Natural: _hands.Any(h => h.Outcome == BjOutcome.Blackjack),
            DoubleWon: _hands.Any(h => h.Doubled && h.Outcome == BjOutcome.Win),
            SplitWon: split && _hands.All(h => h.Outcome is BjOutcome.Win or BjOutcome.Blackjack));
    }
}
