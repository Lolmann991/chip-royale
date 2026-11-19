namespace ChipRoyale.Engine.Cards;

public enum Suit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades,
}

public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace,
}

public readonly record struct Card(Rank Rank, Suit Suit)
{
    private const string RankChars = "23456789TJQKA";
    private const string SuitChars = "cdhs";

    /// <summary>Parses short notation like "As", "Td", "7h".</summary>
    public static Card Parse(string text)
    {
        if (text is null || text.Length != 2)
            throw new FormatException($"Invalid card '{text}'.");

        var r = RankChars.IndexOf(char.ToUpperInvariant(text[0]));
        var s = SuitChars.IndexOf(char.ToLowerInvariant(text[1]));
        if (r < 0 || s < 0)
            throw new FormatException($"Invalid card '{text}'.");

        return new Card((Rank)(r + 2), (Suit)s);
    }

    public static IReadOnlyList<Card> ParseMany(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray();

    public override string ToString() => $"{RankChars[(int)Rank - 2]}{SuitChars[(int)Suit]}";
}
