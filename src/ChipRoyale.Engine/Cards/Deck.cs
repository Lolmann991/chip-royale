namespace ChipRoyale.Engine.Cards;

/// <summary>A standard 52-card deck. Shuffling is always fair (Fisher-Yates).</summary>
public sealed class Deck
{
    private readonly List<Card> _cards;
    private readonly Random _random;

    /// <param name="random">Pass a seeded Random for reproducible games (e.g. seed challenges, tests).</param>
    public Deck(Random? random = null)
    {
        _random = random ?? Random.Shared;
        _cards = new List<Card>(52);
        Reset();
    }

    public int Count => _cards.Count;

    public void Reset()
    {
        _cards.Clear();
        foreach (var suit in Enum.GetValues<Suit>())
            foreach (var rank in Enum.GetValues<Rank>())
                _cards.Add(new Card(rank, suit));
    }

    public void Shuffle()
    {
        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card Draw()
    {
        if (_cards.Count == 0)
            throw new InvalidOperationException("The deck is empty.");

        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    public IReadOnlyList<Card> Draw(int count)
    {
        var result = new Card[count];
        for (var i = 0; i < count; i++)
            result[i] = Draw();
        return result;
    }
}
