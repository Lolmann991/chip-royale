using ChipRoyale.Engine.Cards;

namespace ChipRoyale.Engine.Tests;

public class DeckTests
{
    [Fact]
    public void New_deck_has_52_unique_cards()
    {
        var deck = new Deck();
        var cards = deck.Draw(52);
        Assert.Equal(52, cards.Distinct().Count());
        Assert.Equal(0, deck.Count);
    }

    [Fact]
    public void Same_seed_gives_same_order()
    {
        var a = new Deck(new Random(42));
        var b = new Deck(new Random(42));
        a.Shuffle();
        b.Shuffle();
        Assert.Equal(a.Draw(52), b.Draw(52));
    }

    [Fact]
    public void Card_round_trips_through_text()
    {
        Assert.Equal("Th", Card.Parse("Th").ToString());
        Assert.Equal(new Card(Rank.Ace, Suit.Spades), Card.Parse("As"));
    }
}
