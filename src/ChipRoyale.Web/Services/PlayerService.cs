using System.Security.Claims;
using System.Text.RegularExpressions;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Web.Components.Game;
using ChipRoyale.Web.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using static ChipRoyale.Web.Localization.Loc;

namespace ChipRoyale.Web.Services;

/// <summary>
/// The current player's bankroll for one circuit. Registered users are stored in the
/// database (every change re-reads the row first, so two tabs don't overwrite each
/// other); guests get an in-memory bankroll that is gone after a reload.
/// </summary>
public sealed partial class PlayerService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    AuthenticationStateProvider authState)
{
    private bool loaded;

    public string? UserId { get; private set; }
    public bool IsGuest => UserId is null;
    public string DisplayName { get; private set; } = L("Guest");
    public Bankroll Bankroll { get; private set; } = Bankroll.New();
    public PlayerCollection Collection { get; private set; } = new();

    /// <summary>Raised after achievements were unlocked (their crowns are already paid).</summary>
    public event Action<IReadOnlyList<Achievement>>? AchievementsUnlocked;

    /// <summary>The player's look at the table (equipped avatar).</summary>
    public SeatLook HeroLook
    {
        get
        {
            var avatar = Collection.Equipped(ItemCategory.Avatar);
            return new SeatLook(L("Seat.You"), avatar.Glyph ?? "", 45, avatar.Pixel ? avatar.Id : null);
        }
    }

    /// <summary>CSS classes for the equipped card back, felt and chips (defaults add nothing).</summary>
    public string ThemeClasses => string.Join(' ', new[] { ItemCategory.CardBack, ItemCategory.Felt, ItemCategory.Chips }
        .Select(Collection.Equipped).Where(i => !i.IsDefault).Select(i => i.Id));

    /// <summary>Set when loading found the player broke and started a new run.</summary>
    public bool StartedNewRun { get; private set; }

    /// <summary>Chips returned from a table the player left without cashing out (closed tab).</summary>
    public int ReturnedFromTable { get; private set; }

    public async Task LoadAsync()
    {
        if (loaded) return;
        loaded = true;

        var user = (await authState.GetAuthenticationStateAsync()).User;
        UserId = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        if (UserId is null) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile is null)
        {
            profile = new PlayerProfile
            {
                UserId = UserId,
                DisplayName = L("DefaultName", Random.Shared.Next(1000, 10000)),
                Bankroll = Bankroll.New(),
            };
            db.Profiles.Add(profile);
        }

        if (profile.Bankroll.IsSeated)
        {
            ReturnedFromTable = profile.Bankroll.TableChips;
            profile.Bankroll.CashOut();
        }
        StartedNewRun = profile.Bankroll.ResetIfBroke();
        // Achievements are checked on the next change, when a toast can show them.
        var collection = profile.LoadCollection();
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        DisplayName = profile.DisplayName;
        Bankroll = profile.Bankroll;
        Collection = collection;
    }

    public Task BuyInAsync(TableStake stake, int amount) => MutateAsync(b => b.BuyIn(stake, amount));

    /// <summary>Saves the stack after every hand (and when the player puts chips in).</summary>
    public Task UpdateTableAsync(int stack, HandOutcome? hand = null) => MutateAsync(b =>
    {
        b.SetTableStack(stack);
        if (hand is { } h)
        {
            b.RecordHand(h.Won, h.Pot);
            if (h.TableCleared) b.RecordTableCleared();
        }
    }, hand?.ToEvent());

    public Task EnterTournamentAsync(int buyIn) => MutateAsync(b => b.PayTournamentBuyIn(buyIn));

    /// <summary>Books place and prize of a Sit &amp; Go.</summary>
    /// <returns>True if the player was broke afterwards and a new run started.</returns>
    public async Task<bool> FinishTournamentAsync(int place, int prize)
    {
        var reset = false;
        await MutateAsync(b =>
        {
            b.RecordTournament(place, prize);
            reset = b.ResetIfBroke();
        }, new GameEvent { TournamentPlace = place });
        StartedNewRun = reset;
        return reset;
    }

    /// <summary>Counts a tournament hand for the statistics (no chips move).</summary>
    public Task RecordHandAsync(HandOutcome hand) => MutateAsync(b => b.RecordHand(hand.Won, hand.Pot), hand.ToEvent());

    /// <summary>Exchanges chips for crowns (one way).</summary>
    public Task ExchangeAsync(int chips) => MutateAsync(b => b.Exchange(chips));

    public Task BuyAsync(ShopItem item) => MutateAsync((b, c) => c.Buy(item, b));

    public Task EquipAsync(ShopItem item) => MutateAsync((_, c) => c.Equip(item));

    /// <summary>All tutorial lessons are done (awards the achievement once).</summary>
    public Task CompleteLessonsAsync() => MutateAsync(_ => { }, new GameEvent { LessonsCompleted = true });

    /// <summary>Leaves the table with the given stack.</summary>
    /// <returns>True if the player was broke and a new run started.</returns>
    public async Task<bool> CashOutAsync(int stack)
    {
        var reset = false;
        await MutateAsync(b =>
        {
            b.SetTableStack(stack);
            b.CashOut();
            reset = b.ResetIfBroke();
        });
        StartedNewRun = reset;
        return reset;
    }

    public void DismissNotices()
    {
        StartedNewRun = false;
        ReturnedFromTable = 0;
    }

    /// <returns>An error message, or null on success.</returns>
    public async Task<string?> RenameAsync(string name)
    {
        name = name.Trim();
        if (name.Length is < 3 or > PlayerProfile.MaxNameLength)
            return L("Name.Length", PlayerProfile.MaxNameLength);
        if (!NameRegex().IsMatch(name))
            return L("Name.Chars");
        if (IsGuest)
            return L("Name.Guest");

        await using var db = await dbFactory.CreateDbContextAsync();
        var lower = name.ToLower();
        if (await db.Profiles.AnyAsync(p => p.UserId != UserId && p.DisplayName.ToLower() == lower))
            return L("Name.Taken");
        var profile = await db.Profiles.FirstAsync(p => p.UserId == UserId);
        profile.DisplayName = name;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        DisplayName = name;
        return null;
    }

    private Task MutateAsync(Action<Bankroll> change, GameEvent? e = null) => MutateAsync((b, _) => change(b), e);

    /// <summary>Applies a change, checks achievements and saves (registered players re-read their row first).</summary>
    private async Task MutateAsync(Action<Bankroll, PlayerCollection> change, GameEvent? e = null)
    {
        IReadOnlyList<Achievement> unlocked;
        if (IsGuest)
        {
            // Check before too, so e.g. "10,000 chips" still counts when they are exchanged right away.
            var before = Achievements.Check(Collection, Bankroll, GameEvent.None);
            change(Bankroll, Collection);
            unlocked = [.. before, .. Achievements.Check(Collection, Bankroll, e ?? GameEvent.None)];
        }
        else
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var profile = await db.Profiles.FirstAsync(p => p.UserId == UserId);
            var collection = profile.LoadCollection();
            var before = Achievements.Check(collection, profile.Bankroll, GameEvent.None);
            change(profile.Bankroll, collection);
            unlocked = [.. before, .. Achievements.Check(collection, profile.Bankroll, e ?? GameEvent.None)];
            profile.StoreCollection(collection);
            profile.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            Bankroll = profile.Bankroll;
            Collection = collection;
        }
        if (unlocked.Count > 0) AchievementsUnlocked?.Invoke(unlocked);
    }

    [GeneratedRegex(@"^[\p{L}\p{N} _\-]+$")]
    private static partial Regex NameRegex();
}

/// <param name="WinningHand">The player's hand if they won at showdown.</param>
public readonly record struct HandOutcome(bool Won, int Pot, bool TableCleared, HandCategory? WinningHand = null)
{
    public GameEvent ToEvent() => new() { HandWon = Won, Pot = Pot, WinningHand = WinningHand, TableCleared = TableCleared };
}
