using ChipRoyale.Engine.Economy;
using ChipRoyale.Shared;

namespace ChipRoyale.Client.Tests;

public class DevCommandsTests
{
    private static DevResult Run(PlayerState s, string line) => DevCommands.Execute(s, line, german: true);

    [Fact]
    public void Chips_and_crowns_can_be_set_and_changed()
    {
        var s = PlayerState.New("Test");
        Assert.True(Run(s, "chips 50000").Ok);
        Assert.Equal(50_000, s.Bankroll.Chips);
        Run(s, "chips +5k");
        Assert.Equal(55_000, s.Bankroll.Chips);
        Run(s, "chips -1m");
        Assert.Equal(0, s.Bankroll.Chips);
        Run(s, "crowns 250");
        Assert.Equal(250, s.Bankroll.Crowns);
        Assert.False(Run(s, "chips viele").Ok);
        Assert.True(s.Collection.UsedDevTools);
    }

    [Fact]
    public void Unlock_lock_and_equip_items()
    {
        var s = PlayerState.New("Test");
        var paid = ShopCatalog.All.Where(i => !i.IsDefault).ToList();

        Run(s, "unlock felt");
        Assert.All(paid.Where(i => i.Category == ItemCategory.Felt), i => Assert.True(s.Collection.Owns(i)));
        Assert.DoesNotContain(paid.Where(i => i.Category == ItemCategory.Avatar), s.Collection.Owns);

        Run(s, "unlock all");
        Assert.All(paid, i => Assert.True(s.Collection.Owns(i)));

        var item = paid.First(i => i.Category == ItemCategory.CardBack);
        Run(s, $"equip {item.Id}");
        Assert.True(s.Collection.IsEquipped(item));

        Run(s, $"lock {item.Id}");
        Assert.False(s.Collection.Owns(item));
        Assert.True(s.Collection.Equipped(ItemCategory.CardBack).IsDefault);

        Run(s, "lock all");
        Assert.Empty(s.Collection.Owned);
        Assert.False(Run(s, "unlock gibt-es-nicht").Ok);
    }

    [Fact]
    public void Achievements_and_reset()
    {
        var s = PlayerState.New("Test");
        Run(s, "achieve all");
        Assert.All(Achievements.All, a => Assert.Contains(a.Id, s.Collection.Achievements));
        Assert.Equal(0, s.Bankroll.Crowns); // no reward

        Run(s, "chips 99999");
        Assert.False(Run(s, "reset").Ok); // needs confirm
        Assert.True(Run(s, "reset confirm").Ok);
        Assert.Equal(TableStakes.StartingChips, s.Bankroll.Chips);
        Assert.DoesNotContain(Achievements.All, a => s.Collection.Achievements.Contains(a.Id));
        Assert.True(s.Collection.UsedDevTools); // the mark stays
    }

    [Fact]
    public void Listing_commands_do_not_mark_the_profile()
    {
        var s = PlayerState.New("Test");
        Assert.True(Run(s, "items").Ok);
        Assert.True(Run(s, "stats").Ok);
        Assert.True(Run(s, "achievements").Ok);
        Assert.False(s.Collection.UsedDevTools);
        Assert.False(Run(s, "zaubern").Ok);
    }

    [Fact]
    public void Marker_survives_the_json_round_trip_and_is_no_achievement()
    {
        var s = PlayerState.New("Test");
        Run(s, "crowns 5");
        var copy = PlayerState.FromDto(System.Text.Json.JsonSerializer.Deserialize<ProfileDto>(System.Text.Json.JsonSerializer.Serialize(s.ToDto()))!);
        Assert.True(copy.Collection.UsedDevTools);
        Assert.Null(Achievements.Find(PlayerCollection.DevToolsMarker));
    }
}
