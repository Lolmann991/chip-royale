using System.Security.Claims;
using System.Text.RegularExpressions;
using ChipRoyale.Engine.Economy;
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
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        DisplayName = profile.DisplayName;
        Bankroll = profile.Bankroll;
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
    });

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

    private async Task MutateAsync(Action<Bankroll> change)
    {
        if (IsGuest)
        {
            change(Bankroll);
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var profile = await db.Profiles.FirstAsync(p => p.UserId == UserId);
        change(profile.Bankroll);
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Bankroll = profile.Bankroll;
    }

    [GeneratedRegex(@"^[\p{L}\p{N} _\-]+$")]
    private static partial Regex NameRegex();
}

public readonly record struct HandOutcome(bool Won, int Pot, bool TableCleared);
