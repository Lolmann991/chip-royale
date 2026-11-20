namespace ChipRoyale.Engine.Economy;

public enum ItemCategory
{
    CardBack,
    /// <summary>Card fronts (the whole deck: colours, font, background).</summary>
    CardFront,
    Felt,
    /// <summary>The padded edge of the table.</summary>
    Rail,
    /// <summary>The room around the table (page background).</summary>
    Room,
    /// <summary>Lucky charm standing next to your seat.</summary>
    Charm,
    Avatar,
    Chips,
}

/// <param name="Id">Unique id; also the CSS class that applies the style.</param>
/// <param name="Price">Price in crowns (0 = free default of its category).</param>
/// <param name="Glyph">Emoji for avatars and charms (null for pixel avatars drawn by CSS, and for "no charm").</param>
public sealed record ShopItem(string Id, ItemCategory Category, int Price, bool Pixel = false, string? Glyph = null)
{
    public bool IsDefault => Price == 0;
}

public static class ShopCatalog
{
    public static readonly IReadOnlyList<ShopItem> All =
    [
        // ---- card backs
        new("cb-classic", ItemCategory.CardBack, 0),
        new("cb-royal", ItemCategory.CardBack, 5),
        new("cb-emerald", ItemCategory.CardBack, 5),
        new("cb-sakura", ItemCategory.CardBack, 6),
        new("cb-tartan", ItemCategory.CardBack, 8),
        new("cb-harlequin", ItemCategory.CardBack, 8),
        new("cb-seigaiha", ItemCategory.CardBack, 10),
        new("cb-mosaic", ItemCategory.CardBack, 10),
        new("cb-bauhaus", ItemCategory.CardBack, 12),
        new("cb-lineart", ItemCategory.CardBack, 12),
        new("cb-midnight", ItemCategory.CardBack, 12),
        new("cb-victorian", ItemCategory.CardBack, 14),
        new("cb-mandala", ItemCategory.CardBack, 15),
        new("cb-circuit", ItemCategory.CardBack, 15),
        new("cb-aurora", ItemCategory.CardBack, 18),
        new("cb-galaxy", ItemCategory.CardBack, 18),
        new("cb-synthwave", ItemCategory.CardBack, 20),
        new("cb-marble", ItemCategory.CardBack, 22),
        new("cb-gatsby", ItemCategory.CardBack, 25),
        new("cb-pixel-heart", ItemCategory.CardBack, 15, Pixel: true),
        new("cb-pixel-crown", ItemCategory.CardBack, 20, Pixel: true),
        new("cb-pixel-space", ItemCategory.CardBack, 22, Pixel: true),

        // ---- card fronts (decks)
        new("deck-classic", ItemCategory.CardFront, 0),
        new("deck-fourcolor", ItemCategory.CardFront, 8),
        new("deck-jumbo", ItemCategory.CardFront, 8),
        new("deck-vintage", ItemCategory.CardFront, 14),
        new("deck-noir", ItemCategory.CardFront, 16),
        new("deck-neon", ItemCategory.CardFront, 20),
        new("deck-gold", ItemCategory.CardFront, 25),
        new("deck-pixel", ItemCategory.CardFront, 18, Pixel: true),

        // ---- felts
        new("felt-default", ItemCategory.Felt, 0),
        new("felt-ocean", ItemCategory.Felt, 8),
        new("felt-purple", ItemCategory.Felt, 8),
        new("felt-burgundy", ItemCategory.Felt, 8),
        new("felt-sand", ItemCategory.Felt, 10),
        new("felt-midnight", ItemCategory.Felt, 12),
        new("felt-galaxy", ItemCategory.Felt, 20),
        new("felt-pixel", ItemCategory.Felt, 25, Pixel: true),

        // ---- table rails
        new("rail-wood", ItemCategory.Rail, 0),
        new("rail-oak", ItemCategory.Rail, 10),
        new("rail-leather", ItemCategory.Rail, 12),
        new("rail-marble", ItemCategory.Rail, 20),
        new("rail-gold", ItemCategory.Rail, 25),
        new("rail-neon", ItemCategory.Rail, 25),

        // ---- rooms
        new("room-casino", ItemCategory.Room, 0),
        new("room-saloon", ItemCategory.Room, 15),
        new("room-garden", ItemCategory.Room, 15),
        new("room-montecarlo", ItemCategory.Room, 22),
        new("room-vegas", ItemCategory.Room, 25),
        new("room-space", ItemCategory.Room, 30),

        // ---- lucky charms
        new("charm-none", ItemCategory.Charm, 0),
        new("charm-clover", ItemCategory.Charm, 4, Glyph: "🍀"),
        new("charm-coffee", ItemCategory.Charm, 4, Glyph: "☕"),
        new("charm-dice", ItemCategory.Charm, 5, Glyph: "🎲"),
        new("charm-duck", ItemCategory.Charm, 6, Glyph: "🦆"),
        new("charm-cactus", ItemCategory.Charm, 6, Glyph: "🌵"),
        new("charm-mocktail", ItemCategory.Charm, 7, Glyph: "🍹"),
        new("charm-pig", ItemCategory.Charm, 8, Glyph: "🐷"),
        new("charm-cat", ItemCategory.Charm, 10, Glyph: "🐈‍⬛"),
        new("charm-crystal", ItemCategory.Charm, 12, Glyph: "🔮"),
        new("charm-trophy", ItemCategory.Charm, 14, Glyph: "🏆"),
        new("charm-dragon", ItemCategory.Charm, 15, Glyph: "🐉"),

        // ---- avatars
        new("av-default", ItemCategory.Avatar, 0, Glyph: "😎"),
        new("av-cowboy", ItemCategory.Avatar, 3, Glyph: "🤠"),
        new("av-frog", ItemCategory.Avatar, 3, Glyph: "🐸"),
        new("av-robot", ItemCategory.Avatar, 5, Glyph: "🤖"),
        new("av-ghost", ItemCategory.Avatar, 5, Glyph: "👻"),
        new("av-octopus", ItemCategory.Avatar, 6, Glyph: "🐙"),
        new("av-alien", ItemCategory.Avatar, 8, Glyph: "👽"),
        new("av-lion", ItemCategory.Avatar, 8, Glyph: "🦁"),
        new("av-unicorn", ItemCategory.Avatar, 10, Glyph: "🦄"),
        new("av-ninja", ItemCategory.Avatar, 10, Glyph: "🥷"),
        new("av-vampire", ItemCategory.Avatar, 10, Glyph: "🧛"),
        new("av-wizard", ItemCategory.Avatar, 12, Glyph: "🧙"),
        new("av-dragon", ItemCategory.Avatar, 12, Glyph: "🐲"),
        new("av-king", ItemCategory.Avatar, 15, Glyph: "🤴"),
        new("av-pixel", ItemCategory.Avatar, 20, Pixel: true),

        // ---- chips
        new("chips-classic", ItemCategory.Chips, 0),
        new("chips-pastel", ItemCategory.Chips, 8),
        new("chips-neon", ItemCategory.Chips, 10),
        new("chips-ruby", ItemCategory.Chips, 12),
        new("chips-gold", ItemCategory.Chips, 15),
        new("chips-pixel", ItemCategory.Chips, 15, Pixel: true),
    ];

    public static ShopItem? Find(string id) => All.FirstOrDefault(i => i.Id == id);

    public static ShopItem Default(ItemCategory category) => All.First(i => i.Category == category && i.IsDefault);
}

/// <summary>What a player owns and wears, plus unlocked achievements.</summary>
public sealed class PlayerCollection
{
    private readonly HashSet<string> _owned = [];
    private readonly Dictionary<ItemCategory, string> _equipped = [];
    private readonly HashSet<string> _achievements = [];

    public IReadOnlySet<string> Owned => _owned;
    public IReadOnlySet<string> Achievements => _achievements;

    public bool Owns(ShopItem item) => item.IsDefault || _owned.Contains(item.Id);

    public ShopItem Equipped(ItemCategory category) =>
        _equipped.TryGetValue(category, out var id) && ShopCatalog.Find(id) is { } item ? item : ShopCatalog.Default(category);

    public bool IsEquipped(ShopItem item) => Equipped(item.Category) == item;

    /// <summary>Buys an item with crowns and puts it on.</summary>
    public void Buy(ShopItem item, Bankroll bankroll)
    {
        if (Owns(item)) throw new InvalidOperationException("Already owned.");
        bankroll.SpendCrowns(item.Price);
        _owned.Add(item.Id);
        Equip(item);
    }

    public void Equip(ShopItem item)
    {
        if (!Owns(item)) throw new InvalidOperationException("Not owned.");
        _equipped[item.Category] = item.Id;
    }

    /// <returns>True if the achievement was new.</returns>
    public bool Unlock(string achievementId) => _achievements.Add(achievementId);

    // ------------------------------------------------------------------ storage as plain strings

    public string SerializeOwned() => string.Join(',', _owned.Order());
    public string SerializeEquipped() => string.Join(';', _equipped.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"));
    public string SerializeAchievements() => string.Join(',', _achievements.Order());

    public static PlayerCollection Parse(string? owned, string? equipped, string? achievements)
    {
        var c = new PlayerCollection();
        foreach (var id in Split(owned, ','))
            if (ShopCatalog.Find(id) is not null) c._owned.Add(id);
        foreach (var pair in Split(equipped, ';'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Enum.TryParse<ItemCategory>(parts[0], out var cat)
                && ShopCatalog.Find(parts[1]) is { } item && item.Category == cat && c.Owns(item))
                c._equipped[cat] = item.Id;
        }
        foreach (var id in Split(achievements, ','))
            c._achievements.Add(id);
        return c;
    }

    private static IEnumerable<string> Split(string? s, char sep) =>
        string.IsNullOrWhiteSpace(s) ? [] : s.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
