using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Engine.Tests;

public class ShopTests
{
    private static Bankroll Rich(int chips)
    {
        var b = Bankroll.New();
        b.BuyIn(TableStakes.Find("pub")!, 1000);
        b.SetTableStack(chips);
        b.CashOut();
        return b;
    }

    [Fact]
    public void Only_chips_above_the_starting_chips_can_be_exchanged()
    {
        var b = Rich(3450);
        Assert.Equal(2400, b.ExchangeableChips); // 3450 - 1000, rounded down to 100
        Assert.Equal(24, b.Exchange(2400));
        Assert.Equal(24, b.Crowns);
        Assert.Equal(1050, b.Chips);
        Assert.Equal(0, b.ExchangeableChips);
        Assert.Throws<InvalidOperationException>(() => b.Exchange(100));
        Assert.Throws<ArgumentException>(() => Rich(5000).Exchange(150));
        Assert.Equal(0, Bankroll.New().ExchangeableChips);
    }

    [Fact]
    public void Crowns_survive_going_broke()
    {
        var b = Rich(2000);
        b.Exchange(1000);
        b.BuyIn(TableStakes.Find("pub")!, 1000);
        b.SetTableStack(0);
        Assert.True(b.ResetIfBroke());
        Assert.Equal(10, b.Crowns);
    }

    [Fact]
    public void Buying_spends_crowns_and_equips()
    {
        var b = Rich(3000);
        b.Exchange(2000); // 20 crowns
        var c = new PlayerCollection();
        var item = ShopCatalog.Find("cb-midnight")!;
        Assert.Equal("cb-classic", c.Equipped(ItemCategory.CardBack).Id);

        c.Buy(item, b);
        Assert.Equal(8, b.Crowns);
        Assert.True(c.Owns(item));
        Assert.True(c.IsEquipped(item));
        Assert.Throws<InvalidOperationException>(() => c.Buy(item, b));
        Assert.Throws<InvalidOperationException>(() => c.Buy(ShopCatalog.Find("felt-pixel")!, b)); // too expensive

        c.Equip(ShopCatalog.Default(ItemCategory.CardBack));
        Assert.Equal("cb-classic", c.Equipped(ItemCategory.CardBack).Id);
    }

    [Fact]
    public void Collection_round_trips_through_strings_and_ignores_garbage()
    {
        var b = Rich(10_000);
        b.Exchange(9000);
        var c = new PlayerCollection();
        c.Buy(ShopCatalog.Find("felt-ocean")!, b);
        c.Buy(ShopCatalog.Find("av-robot")!, b);
        c.Unlock("first-win");

        var copy = PlayerCollection.Parse(c.SerializeOwned(), c.SerializeEquipped() + ";Felt=unknown;Chips=chips-gold", c.SerializeAchievements());
        Assert.Equal(c.Owned, copy.Owned);
        Assert.Equal("felt-ocean", copy.Equipped(ItemCategory.Felt).Id);
        Assert.Equal("av-robot", copy.Equipped(ItemCategory.Avatar).Id);
        Assert.Equal("chips-classic", copy.Equipped(ItemCategory.Chips).Id); // not owned → default
        Assert.Contains("first-win", copy.Achievements);
    }

    [Fact]
    public void Every_category_has_exactly_one_free_default_and_ids_are_unique()
    {
        foreach (var cat in Enum.GetValues<ItemCategory>())
            Assert.Single(ShopCatalog.All, i => i.Category == cat && i.IsDefault);
        Assert.Equal(ShopCatalog.All.Count, ShopCatalog.All.Select(i => i.Id).Distinct().Count());
        Assert.All(ShopCatalog.All.Where(i => i.Category == ItemCategory.Avatar && !i.Pixel), i => Assert.NotNull(i.Glyph));
    }

    [Fact]
    public void Achievements_unlock_once_and_pay_crowns()
    {
        var b = Bankroll.New();
        var c = new PlayerCollection();
        var won = new GameEvent { HandWon = true, Pot = 300, WinningHand = HandCategory.FullHouse };

        var first = Achievements.Check(c, b, won);
        Assert.Equal(["first-win", "full-house"], first.Select(a => a.Id));
        Assert.Equal(5, b.Crowns);
        Assert.Empty(Achievements.Check(c, b, won));

        var sng = Achievements.Check(c, b, new GameEvent { TournamentPlace = 1 });
        Assert.Equal(["sng-cash", "sng-win"], sng.Select(a => a.Id));
        Assert.Equal(25, b.Crowns);
    }
}
