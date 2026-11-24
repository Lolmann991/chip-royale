using ChipRoyale.Client.Components.Game;
using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Shared;
using static ChipRoyale.Client.Localization.Loc;

namespace ChipRoyale.Client.Services;

public enum PlayerMode
{
    /// <summary>Logged in and the server is reachable: everything is stored in the database.</summary>
    Online,
    /// <summary>Not logged in: the profile is stored in this browser.</summary>
    Guest,
    /// <summary>The server cannot be reached: the game keeps going with the profile in this browser.</summary>
    Offline,
}

/// <summary>
/// The current player's chips, crowns and items. Uses the server (database) when logged in and
/// online, otherwise a profile stored in the browser (localStorage). Both follow the same rules
/// (<see cref="ProfileOperations"/>).
/// </summary>
public sealed class PlayerService(GameServer server, BrowserStorage storage)
{
    private const string LocalKey = "cr-local-profile";

    /// <summary>How often the server is checked in the background (found after starting it, gone after stopping it).</summary>
    private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(8);

    private bool loaded;
    private bool connectionLost;
    private int pending;
    private PlayerState state = PlayerState.New("");

    public PlayerMode Mode => UsesServer ? PlayerMode.Online : server.IsOnline ? PlayerMode.Guest : PlayerMode.Offline;

    /// <summary>Not stored on the server (guest or offline profile).</summary>
    public bool IsGuest => !UsesServer;

    public bool IsLoggedIn => server.IsLoggedIn;
    public string? Email => server.Email;
    public string ServerUrl => server.DisplayUrl;

    /// <summary>A server address is set up (otherwise the game is offline on purpose).</summary>
    public bool HasServer => server.HasServer;

    /// <summary>The server went away during this session; progress is no longer saved until reloading.</summary>
    public bool ConnectionLost => connectionLost;

    public string DisplayName => state.DisplayName;
    public Bankroll Bankroll => state.Bankroll;
    public PlayerCollection Collection => state.Collection;
    public CasinoStats Casino => state.Casino;

    /// <summary>Different games played (for the statistics).</summary>
    public int GamesPlayed => state.GamesPlayed;

    /// <summary>Set when loading found the player broke and started a new run.</summary>
    public bool StartedNewRun { get; private set; }

    /// <summary>Chips returned from a table the player left without cashing out (closed tab).</summary>
    public int ReturnedFromTable { get; private set; }

    /// <summary>Raised after achievements were unlocked (their crowns are already paid).</summary>
    public event Action<IReadOnlyList<Achievement>>? AchievementsUnlocked;

    /// <summary>Raised when the mode or the profile changes (login, logout, server lost or found).</summary>
    public event Action? Changed;

    /// <summary>
    /// A table or tournament is running. The background check then does not switch between the
    /// server and the browser profile (that would mix two bankrolls mid-game).
    /// </summary>
    public bool InGame { get; set; }

    private bool UsesServer => server.IsOnline && server.IsLoggedIn && !connectionLost;

    /// <summary>The player's look at the table (equipped avatar).</summary>
    public SeatLook HeroLook
    {
        get
        {
            var avatar = Collection.Equipped(ItemCategory.Avatar);
            return new SeatLook(L("Seat.You"), avatar.Glyph ?? "", 45, avatar.Pixel ? avatar.Id : null);
        }
    }

    /// <summary>CSS classes for the equipped styles (defaults add nothing).</summary>
    public string ThemeClasses => string.Join(' ', new[]
        {
            ItemCategory.CardBack, ItemCategory.CardFront, ItemCategory.Felt, ItemCategory.Rail, ItemCategory.Room, ItemCategory.Chips,
        }
        .Select(Collection.Equipped).Where(i => !i.IsDefault).Select(i => i.Id));

    /// <summary>Emoji of the equipped lucky charm, or null.</summary>
    public string? Charm => Collection.Equipped(ItemCategory.Charm).Glyph;

    // ---------------------------------------------------------------- loading & accounts

    public async Task LoadAsync()
    {
        if (loaded) return;
        loaded = true;
        await server.InitializeAsync();
        await ReloadAsync();
        _ = WatchServerAsync();
    }

    /// <summary>
    /// Checks the server every few seconds: a server started after the game was opened is used
    /// automatically (account and values from the database), a stopped one switches to offline mode.
    /// </summary>
    private async Task WatchServerAsync()
    {
        using var timer = new PeriodicTimer(WatchInterval);
        while (await timer.WaitForNextTickAsync())
        {
            if (!server.HasServer || InGame || pending > 0) continue;
            try
            {
                var wasOnline = server.IsOnline && !connectionLost;
                var online = await server.CheckAsync();
                if (online == wasOnline || InGame || pending > 0) continue;
                connectionLost = false;
                await ReloadAsync();
            }
            catch (Exception)
            {
                // Never let the background check break the game.
            }
        }
    }

    /// <summary>Tries the server again (after it was offline) and reloads the profile.</summary>
    public async Task ReconnectAsync()
    {
        connectionLost = false;
        await server.CheckAsync();
        await ReloadAsync();
    }

    public async Task SetServerAsync(string url)
    {
        await server.SetBaseUrlAsync(url);
        connectionLost = false;
        await ReloadAsync();
    }

    /// <returns>Error messages (empty on success).</returns>
    public async Task<IReadOnlyList<string>> RegisterAsync(string email, string password)
    {
        if (!await server.CheckAsync()) return [L("Account.ServerOffline")];
        var codes = await server.RegisterAsync(email, password);
        if (codes.Count > 0)
            return [.. codes.Select(c => Localization.ValidationTexts.IdentityError(c, email)).Distinct()];
        return await LoginAsync(email, password) ? [] : [L("IdentityError.DefaultError")];
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        if (!await server.CheckAsync()) return false;
        if (!await server.LoginAsync(email, password)) return false;
        connectionLost = false;
        await ReloadAsync();
        return true;
    }

    public async Task LogoutAsync()
    {
        await server.LogoutAsync();
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        StartedNewRun = false;
        ReturnedFromTable = 0;
        if (UsesServer)
        {
            try
            {
                Apply(await server.GetAsync<ProfileResponse>(ApiRoutes.Profile));
                Changed?.Invoke();
                return;
            }
            catch (UnauthorizedAccessException) { }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                connectionLost = true;
            }
        }

        var dto = await storage.GetAsync<ProfileDto>(LocalKey);
        state = dto is null ? PlayerState.New(L("Guest")) : PlayerState.FromDto(dto);
        var result = ProfileOperations.Load(state);
        StartedNewRun = result.StartedNewRun;
        ReturnedFromTable = result.ReturnedFromTable;
        await SaveLocalAsync();
        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- operations

    public Task BuyInAsync(TableStake stake, int amount) => RunAsync(
        s => ProfileOperations.BuyIn(s, stake.Id, amount), ApiRoutes.BuyIn, new BuyInRequest(stake.Id, amount));

    /// <summary>Saves the stack after every hand (and when the player puts chips in).</summary>
    public Task UpdateTableAsync(int stack, HandOutcome? hand = null)
    {
        var dto = hand?.ToDto();
        return RunAsync(s => ProfileOperations.Table(s, stack, dto), ApiRoutes.Table, new TableRequest(stack, dto));
    }

    /// <returns>True if the player was broke and a new run started.</returns>
    public async Task<bool> CashOutAsync(int stack)
    {
        await RunAsync(s => ProfileOperations.CashOut(s, stack), ApiRoutes.CashOut, new CashOutRequest(stack));
        return StartedNewRun;
    }

    /// <summary>A blackjack round is over: stack at the table and statistics.</summary>
    public Task BlackjackRoundAsync(int stack, Engine.Blackjack.BjRoundResult round)
    {
        var dto = new BlackjackRoundDto(round.Net, round.Natural, round.DoubleWon, round.SplitWon);
        return RunAsync(s => ProfileOperations.Blackjack(s, stack, dto), ApiRoutes.Blackjack, new BlackjackRequest(stack, dto));
    }

    /// <summary>A round of roulette, slots or video poker is over.</summary>
    public Task CasinoRoundAsync(int stack, CasinoRoundDto round) => RunAsync(
        s => ProfileOperations.CasinoRound(s, stack, round), ApiRoutes.CasinoRound, new CasinoRequest(stack, round));

    public bool DailyAvailable => state.Casino.DailyAvailable(DateOnly.FromDateTime(DateTime.Now));

    /// <summary>Spins the daily bonus wheel (once per day).</summary>
    /// <returns>The wheel segment and the chips won, or an error.</returns>
    public async Task<(int Segment, int Chips, string? Error)> DailyBonusAsync()
    {
        var chipsBefore = state.Bankroll.Chips;
        pending++;
        try
        {
            if (UsesServer)
            {
                try
                {
                    var response = await server.PostAsync<ProfileResponse>(ApiRoutes.DailyBonus, new { });
                    if (response is null) return (0, 0, L("Error.Request"));
                    Apply(response, keepNotices: true);
                    return response.Error is { } e ? (0, 0, L(e)) : (response.Value ?? 0, state.Bankroll.Chips - chipsBefore, null);
                }
                catch (UnauthorizedAccessException)
                {
                    await ReloadAsync();
                    return (0, 0, L("Account.SessionExpired"));
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    connectionLost = true;
                    Changed?.Invoke();
                    return (0, 0, L("Conn.Lost"));
                }
            }

            var result = ProfileOperations.DailyBonus(state, DateOnly.FromDateTime(DateTime.Now), Random.Shared);
            if (result.Error is { } error) return (0, 0, L(error));
            Raise(result.Unlocked);
            if (!connectionLost) await SaveLocalAsync();
            return (result.Value ?? 0, state.Bankroll.Chips - chipsBefore, null);
        }
        finally
        {
            pending--;
        }
    }

    public Task EnterTournamentAsync(string tierId) => RunAsync(
        s => ProfileOperations.TournamentEnter(s, tierId), ApiRoutes.TournamentEnter, new TournamentEnterRequest(tierId));

    /// <summary>Books place and prize of a Sit &amp; Go (the prize follows from tier and place).</summary>
    public async Task<bool> FinishTournamentAsync(string tierId, int place)
    {
        await RunAsync(s => ProfileOperations.TournamentFinish(s, tierId, place), ApiRoutes.TournamentFinish,
            new TournamentFinishRequest(tierId, place));
        return StartedNewRun;
    }

    /// <summary>Counts a tournament hand for the statistics (no chips move).</summary>
    public Task RecordHandAsync(HandOutcome hand)
    {
        var dto = hand.ToDto();
        return RunAsync(s => ProfileOperations.Hand(s, dto), ApiRoutes.Hand, dto);
    }

    public Task ExchangeAsync(int chips) => RunAsync(
        s => ProfileOperations.Exchange(s, chips), ApiRoutes.Exchange, new ExchangeRequest(chips));

    public Task BuyAsync(ShopItem item) => RunAsync(
        s => ProfileOperations.Buy(s, item.Id), ApiRoutes.Buy, new ItemRequest(item.Id));

    public Task EquipAsync(ShopItem item) => RunAsync(
        s => ProfileOperations.Equip(s, item.Id), ApiRoutes.Equip, new ItemRequest(item.Id));

    /// <summary>All tutorial lessons are done (awards the achievement once).</summary>
    public Task CompleteLessonsAsync() => RunAsync(ProfileOperations.LessonsCompleted, ApiRoutes.Lessons, null);

    /// <returns>An error message, or null on success.</returns>
    public async Task<string?> RenameAsync(string name)
    {
        if (NameRules.Check(name) is { } key) return key == "Name.Length" ? L(key, NameRules.MaxLength) : L(key);
        var error = await RunAsync(s => ProfileOperations.Rename(s, name), ApiRoutes.Rename, new RenameRequest(name));
        return error;
    }

    /// <summary>
    /// Runs a developer console command on the profile: on the server when online (if the server
    /// allows it), otherwise on the profile in this browser.
    /// </summary>
    public async Task<DevResult> DevAsync(string command)
    {
        var german = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        pending++;
        try
        {
            if (UsesServer)
            {
                try
                {
                    var response = await server.PostAsync<DevResponse>(ApiRoutes.Dev, new DevRequest(command, german ? "de" : "en"));
                    if (response is null) return new DevResult(new OperationResult([]), [L("Error.Request")], false);
                    Apply(response.Profile, keepNotices: true);
                    Changed?.Invoke();
                    return new DevResult(new OperationResult(response.Profile.Unlocked), response.Output, response.Ok);
                }
                catch (UnauthorizedAccessException)
                {
                    await ReloadAsync();
                    return new DevResult(new OperationResult([]), [L("Account.SessionExpired")], false);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    connectionLost = true;
                    Changed?.Invoke();
                    return new DevResult(new OperationResult([]), [L("Conn.Lost")], false);
                }
            }

            var result = DevCommands.Execute(state, command, german);
            if (!connectionLost) await SaveLocalAsync();
            Changed?.Invoke();
            return result;
        }
        finally
        {
            pending--;
        }
    }

    public void DismissNotices()
    {
        StartedNewRun = false;
        ReturnedFromTable = 0;
    }

    /// <summary>Runs an operation on the server, or locally (guest / offline).</summary>
    /// <returns>The translated error, if the operation was refused.</returns>
    private async Task<string?> RunAsync(Func<PlayerState, OperationResult> local, string route, object? body)
    {
        pending++;
        try
        {
            return await RunCoreAsync(local, route, body);
        }
        finally
        {
            pending--;
        }
    }

    private async Task<string?> RunCoreAsync(Func<PlayerState, OperationResult> local, string route, object? body)
    {
        if (UsesServer)
        {
            try
            {
                var response = await server.PostAsync<ProfileResponse>(route, body ?? new { });
                Apply(response, keepNotices: true);
                return response?.Error is { } e ? L(e) : null;
            }
            catch (UnauthorizedAccessException)
            {
                await ReloadAsync();
                return L("Account.SessionExpired");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Server gone: keep playing with what we have, but nothing is saved any more.
                connectionLost = true;
                Changed?.Invoke();
            }
        }

        var result = local(state);
        var r = result.ToResponse(state);
        if (result.StartedNewRun) StartedNewRun = true;
        Raise(r.Unlocked);
        if (!connectionLost) await SaveLocalAsync();
        return result.Error is { } error ? L(error) : null;
    }

    private void Apply(ProfileResponse? response, bool keepNotices = false)
    {
        if (response is null) return;
        state = PlayerState.FromDto(response.Profile);
        if (!keepNotices)
        {
            StartedNewRun = response.StartedNewRun;
            ReturnedFromTable = response.ReturnedFromTable;
        }
        else if (response.StartedNewRun)
        {
            StartedNewRun = true;
        }
        Raise(response.Unlocked);
    }

    private void Raise(string[] unlocked)
    {
        var list = unlocked.Select(Achievements.Find).OfType<Achievement>().ToList();
        if (list.Count > 0) AchievementsUnlocked?.Invoke(list);
    }

    private ValueTask SaveLocalAsync() => storage.SetAsync(LocalKey, state.ToDto());
}

/// <param name="WinningHand">The player's hand if they won at showdown.</param>
public readonly record struct HandOutcome(bool Won, int Pot, bool TableCleared, HandCategory? WinningHand = null)
{
    public HandOutcomeDto ToDto() => new(Won, Pot, TableCleared, WinningHand);
}
