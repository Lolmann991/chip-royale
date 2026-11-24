using ChipRoyale.Engine.Ai;
using ChipRoyale.Engine.Cards;
using ChipRoyale.Engine.Evaluation;
using ChipRoyale.Engine.Holdem;
using ChipRoyale.Engine.Tournament;
using ChipRoyale.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static ChipRoyale.Client.Localization.Loc;

namespace ChipRoyale.Client.Components.Game;

/// <summary>Game loop and state of the poker table; the markup lives in PokerTable.razor.</summary>
public partial class PokerTable
{
    [Parameter, EditorRequired] public GameSettings Settings { get; set; } = default!;
    /// <summary>Leaving the table; the argument is the hero's stack to cash out.</summary>
    [Parameter] public EventCallback<int> OnExit { get; set; }

    /// <summary>Stack changes and finished hands, so the bankroll can be saved (not raised at the practice table).</summary>
    [Parameter] public EventCallback<TableReport> OnReport { get; set; }

    /// <summary>A Sit &amp; Go is over for the player (knocked out, won, or left).</summary>
    [Parameter] public EventCallback<TournamentResult> OnTournamentFinished { get; set; }

    /// <summary>Name and avatar of the player (equipped shop avatar).</summary>
    [Parameter] public SeatLook? HeroLook { get; set; }

    /// <summary>Emoji of the player's lucky charm on the table (shop item), or null.</summary>
    [Parameter] public string? Charm { get; set; }

    // Seat slots around the oval (percent of the table box).
    private static readonly (double X, double Y)[] SlotPos = [(50, 101), (2.5, 66), (17, 7), (50, -1), (83, 7), (97.5, 66)];
    private static readonly int[][] SlotMap = [[], [], [0, 3], [0, 2, 4], [0, 1, 3, 5], [0, 1, 2, 4, 5], [0, 1, 2, 3, 4, 5]];

    private readonly Random random = new();
    private HoldemTable table = default!;
    private BotBrain?[] brains = [];
    private SeatLook[] looks = [];
    private int[] slots = [];

    private CancellationTokenSource cts = new();
    private CancellationTokenSource? autoCts;
    private IJSObjectReference? js;
    private DotNetObjectReference<PokerTable>? selfRef;

    private bool loopRunning;
    private bool heroTurn;
    private LegalActions? legal;
    private int raiseTo;
    private int thinkingSeat = -1;
    private bool collecting;
    private bool revealCards;
    private bool finishingHand;
    private bool peek;
    private bool hints;
    private bool sound = true;
    private bool showLog;
    private bool showRankings;
    private bool exiting;
    private HandOdds? heroOdds;
    private int? startingRank;
    private double? startingTop;
    private bool sweeping;
    private bool autoPaused;
    private (int Hand, int Board, int Active) equityKey;
    private int potOverride = -1;
    private HandResult? lastResult;
    private HashSet<int> winners = [];
    private HashSet<Card> winningCards = [];
    private Banner? banner;
    private int autoContinueMs;
    private bool? gameOver;
    private readonly List<LogLine> log = [];
    private SitAndGo? sng;
    private TournamentResult? tournamentResult;
    private string? levelNotice;

    private int handsPlayed, handsWon, biggestPot, peakChips;

    private sealed record Banner(string Title, string Sub, bool Win);
    private sealed record LogLine(string Text, string Kind = "");

    private Player Hero => table.Players[0];

    private int PotAmount => potOverride >= 0 ? potOverride
        : table.Phase == TablePhase.HandComplete ? 0
        : collecting ? table.Pot : table.CollectedPot;

    private int RaiseUnit => Math.Max(1, table.SmallBlind);

    private HandCategory? CurrentCategory => Hero.HoleCards.Count == 2 && table.Board.Count >= 3 && !Hero.HasFolded
        ? HandEvaluator.EvaluateBest([.. Hero.HoleCards, .. table.Board]).Category
        : Hero.HoleCards.Count == 2 && Hero.HoleCards[0].Rank == Hero.HoleCards[1].Rank && !Hero.HasFolded ? HandCategory.OnePair : null;

    protected override void OnInitialized() => SetupGame(Settings.Tournament?.StartingStack ?? Settings.BuyIn);

    private void SetupGame(int heroStack)
    {
        var bots = Roster.PickBots(Settings.Opponents, random);
        looks = [HeroLook ?? Roster.Human, .. bots];
        var players = looks.Select((l, i) => new Player(l.Name, i == 0 ? heroStack : BotStack(), isHuman: i == 0)).ToList();
        table = new HoldemTable(players, Settings.SmallBlind, Settings.BigBlind);
        brains = looks.Select((_, i) => i == 0 ? null
            : new BotBrain(new BotProfile(Settings.Difficulty, random.NextDouble(), random.NextDouble()))).ToArray();
        slots = SlotMap[players.Count];
        sng = Settings.Tournament is { } config ? new SitAndGo(config, table) : null;
        tournamentResult = null;
        levelNotice = null;
        hints = Settings.Difficulty == Difficulty.Easy;
        handsPlayed = handsWon = biggestPot = 0;
        peakChips = heroStack;
        gameOver = null;
        banner = null;
        log.Clear();
        ResetHandVisuals();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        try
        {
            js = await JS.InvokeAsync<IJSObjectReference>("import", "./js/table.js");
            selfRef = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("init", selfRef);
            sound = await js.InvokeAsync<bool>("getSound");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException) { }

        await Task.Delay(400);
        await NextHandAsync();
    }

    // ------------------------------------------------------------------ game loop

    private async Task NextHandAsync()
    {
        if (sweeping || gameOver is not null || finishingHand || table.Phase is TablePhase.WaitingForAction or TablePhase.RoundComplete) return;
        if (!table.CanStartHand) return;
        autoCts?.Cancel();

        // Gather the old cards back to the deck before dealing again.
        if (table.HandNumber > 0)
        {
            sweeping = true;
            banner = null;
            StateHasChanged();
            await Delay(480);
            sweeping = false;
        }

        ResetHandVisuals();
        levelNotice = null;
        if (sng is not null && sng.BeforeHand())
        {
            levelNotice = L("Sng.LevelUp", table.SmallBlind, table.BigBlind);
            log.Add(new LogLine(levelNotice, "street"));
            Sfx("allin");
        }
        table.StartHand();
        log.Add(new LogLine(L("Table.HandHeader", table.HandNumber), "hand"));
        Sfx("card");
        await UpdateEquityAsync();
        StateHasChanged();
        await Delay(900);
        await RunAsync();
    }

    private void ResetHandVisuals()
    {
        winners = [];
        winningCards = [];
        lastResult = null;
        banner = null;
        revealCards = false;
        collecting = false;
        peek = false;
        potOverride = -1;
        heroOdds = null;
        startingRank = null;
        startingTop = null;
        autoPaused = false;
        heroTurn = false;
        legal = null;
        autoContinueMs = 0;
    }

    private async Task RunAsync()
    {
        if (loopRunning) return;
        loopRunning = true;
        try
        {
            var token = cts.Token;
            while (!token.IsCancellationRequested)
            {
                if (table.Phase == TablePhase.RoundComplete)
                {
                    revealCards |= table.IsRunout;
                    StateHasChanged();
                    await Delay(table.IsRunout ? 900 : 500);

                    var hadBets = table.Players.Any(p => p.StreetBet > 0);
                    if (hadBets)
                    {
                        collecting = true;
                        Sfx("bet");
                        StateHasChanged();
                        await Delay(560);
                    }

                    var boardBefore = table.Board.Count;
                    table.Advance();
                    collecting = false;
                    if (table.Board.Count > boardBefore)
                    {
                        Sfx("card");
                        log.Add(new LogLine($"{HandText.Street(table.Street)}: {string.Join(" ", table.Board.Skip(boardBefore).Select(CardText))}", "street"));
                    }
                    if (hadBets) Bump("pot-label");

                    if (table.Phase == TablePhase.HandComplete)
                    {
                        await HandCompleteAsync();
                        return;
                    }
                    await UpdateEquityAsync();
                    StateHasChanged();
                    await Delay(table.Board.Count == 3 ? 900 : 650);
                    continue;
                }

                if (table.Phase != TablePhase.WaitingForAction || table.ActingIndex is not { } idx)
                    return;

                if (table.Players[idx].IsHuman)
                {
                    BeginHeroTurn();
                    return;
                }

                // Bot turn: think (Monte-Carlo runs off the UI thread) and act.
                thinkingSeat = idx;
                StateHasChanged();
                var brain = brains[idx]!;
                var decide = Task.Run(() => brain.Decide(table), token);
                var think = Delay(Hero.IsActive ? random.Next(650, 1300) : random.Next(300, 600));
                await Task.WhenAll(decide, think);
                thinkingSeat = -1;
                if (token.IsCancellationRequested) return;

                ApplyAction(idx, decide.Result);
                if (table.Phase == TablePhase.HandComplete)
                {
                    await HandCompleteAsync();
                    return;
                }
                await UpdateEquityAsync();
                StateHasChanged();
                await Delay(Hero.IsActive ? 380 : 200);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            loopRunning = false;
        }
    }

    private void ApplyAction(int idx, PlayerAction action)
    {
        var applied = table.Act(action);
        log.Add(new LogLine($"{looks[idx].Name}: {HandText.Action(applied)}"));
        Sfx(applied.Type switch
        {
            ActionType.Fold => "fold",
            ActionType.Check => "check",
            ActionType.AllIn => "allin",
            ActionType.Raise => "bet",
            _ => "chip",
        });
    }

    private void BeginHeroTurn()
    {
        legal = table.GetLegalActions();
        heroTurn = true;
        var suggested = legal.CurrentBet == 0
            ? (int)((legal.Pot) * 0.66)
            : legal.CurrentBet + (int)((legal.Pot + legal.ToCall) * 0.66);
        SetRaise(Math.Max(legal.MinRaiseTo, suggested));
        Sfx("turn");
        StateHasChanged();
    }

    private async Task HeroActAsync(PlayerAction action)
    {
        if (!heroTurn || table.ActingIndex != 0) return;
        heroTurn = false;
        legal = null;
        var before = Hero.Chips;
        ApplyAction(0, action);
        if (Hero.Chips != before && table.Phase != TablePhase.HandComplete)
            await ReportAsync(null);
        if (table.Phase == TablePhase.HandComplete)
        {
            await HandCompleteAsync();
            return;
        }
        await UpdateEquityAsync();
        StateHasChanged();
        await Delay(300);
        await RunAsync();
    }

    private Task HeroRaiseAsync() => legal is null ? Task.CompletedTask : HeroActAsync(BetControls.RaiseFor(legal, raiseTo));

    private async Task HandCompleteAsync()
    {
        finishingHand = true;
        try
        {
            await ShowHandResultAsync();
        }
        finally
        {
            finishingHand = false;
        }
    }

    private async Task ShowHandResultAsync()
    {
        var result = table.LastResult!;
        lastResult = result;
        handsPlayed++;
        biggestPot = Math.Max(biggestPot, result.TotalPot);
        potOverride = result.TotalPot;
        revealCards = result.WasShowdown;
        winners = result.WinnerIndices.ToHashSet();
        if (result.WasShowdown)
        {
            foreach (var pot in result.Pots)
                foreach (var w in pot.Winners)
                    winningCards.UnionWith(result.Hands[w].Cards);
        }

        var heroWon = result.Winnings.GetValueOrDefault(0);
        var mainWinner = result.Winnings.OrderByDescending(w => w.Value).First().Key;
        var mainHand = result.WasShowdown ? result.Hands[mainWinner] : null;
        var how = mainHand is null ? L("Table.EveryoneFolded") : L("Table.WithHand", HandText.Describe(mainHand));

        if (heroWon > 0)
        {
            handsWon++;
            var split = winners.Count > 1 ? L("Table.Split") : "";
            banner = new Banner(L("Table.YouWin", heroWon, split), how, true);
        }
        else
        {
            var names = string.Join(L("Table.And"), winners.Select(w => looks[w].Name));
            banner = new Banner(L(winners.Count > 1 ? "Table.OthersWin" : "Table.OtherWins", names, result.Winnings[mainWinner]), how, false);
        }

        foreach (var w in winners)
            log.Add(new LogLine(L("Table.LogWin", looks[w].Name, result.Winnings[w]) + (result.WasShowdown ? $" – {HandText.Describe(result.Hands[w])}" : ""), "win"));

        StateHasChanged();
        await Delay(result.WasShowdown ? 1100 : 450);

        // Chips fly from the pot to the winner(s).
        var fly = new List<Task>();
        foreach (var w in winners)
            fly.Add(JsAsync("flyChips", "pot", $"seat-{w}", Math.Clamp(result.Winnings[w] / Math.Max(1, table.BigBlind), 6, 22)));
        potOverride = 0;
        StateHasChanged();

        if (heroWon > 0)
        {
            var category = mainHand?.Category ?? HandCategory.HighCard;
            var bigPot = result.TotalPot >= Settings.BuyIn;
            var level = category >= HandCategory.FullHouse || bigPot ? 3
                : category >= HandCategory.Straight || result.TotalPot >= table.BigBlind * 15 ? 2 : 1;
            Sfx(level >= 2 ? "bigwin" : "win");
            _ = JsAsync("celebrate", level);
            if (table.Log.Any(e => e.HandNumber == table.HandNumber && e.Action.Type == ActionType.AllIn))
                _ = JsAsync("shake");
        }
        else if (Hero.TotalInvested > 0 && result.WasShowdown && Hero.IsActive)
        {
            Sfx("lose");
        }

        await Task.WhenAll(fly);
        peakChips = Math.Max(peakChips, Hero.Chips);
        var cleared = Hero.Chips > 0 && table.Players.Skip(1).All(p => p.Chips == 0);
        if (Hero.InHand)
            await ReportAsync(new HandOutcome(heroWon > 0, result.TotalPot, cleared,
                heroWon > 0 && result.Hands.TryGetValue(0, out var heroHand) ? heroHand.Category : null));

        if (sng is not null)
        {
            foreach (var busted in sng.AfterHand().Where(i => i != 0))
                log.Add(new LogLine(L("Sng.KnockedOut", looks[busted].Name, sng.PlaceOf(busted) ?? 0), "street"));
            if (Hero.Chips == 0 || sng.IsFinished)
            {
                await FinishTournamentAsync(sng.PlaceOf(0) ?? sng.PlayersLeft);
                StateHasChanged();
                return;
            }
            autoContinueMs = ResultTime(heroWon > 0 ? 13000 : 9000);
            ScheduleAutoContinue(table.HandNumber, autoContinueMs);
        }
        else if (Hero.Chips == 0)
        {
            gameOver = false;
            Sfx("lose");
        }
        else if (table.Players.Skip(1).All(p => p.Chips == 0))
        {
            gameOver = true;
            Sfx("bigwin");
            _ = JsAsync("celebrate", 3);
        }
        else
        {
            autoContinueMs = ResultTime(heroWon > 0 ? 14000 : 10000);
            ScheduleAutoContinue(table.HandNumber, autoContinueMs);
        }
        StateHasChanged();
    }

    private void ScheduleAutoContinue(int hand, int ms)
    {
        autoCts?.Cancel();
        autoCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var token = autoCts.Token;
        _ = InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(ms, token);
                if (table.HandNumber == hand && table.Phase == TablePhase.HandComplete) await NextHandAsync();
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task RestartAsync()
    {
        autoCts?.Cancel();
        SetupGame(Settings.BuyIn);
        StateHasChanged();
        await Delay(300);
        await NextHandAsync();
    }

    private async Task NewOpponentsAsync()
    {
        autoCts?.Cancel();
        SetupGame(Hero.Chips);
        StateHasChanged();
        await Delay(300);
        await NextHandAsync();
    }

    private async Task FinishTournamentAsync(int place)
    {
        if (sng is null || tournamentResult is not null) return;
        var result = new TournamentResult(place, sng.Config.Prize(place));
        tournamentResult = result;
        gameOver = place == 1;
        autoCts?.Cancel();
        if (place == 1)
        {
            Sfx("bigwin");
            _ = JsAsync("celebrate", 3);
        }
        else if (result.Prize > 0)
        {
            Sfx("win");
            _ = JsAsync("celebrate", 2);
        }
        else
        {
            Sfx("lose");
        }
        await OnTournamentFinished.InvokeAsync(result);
    }

    private async Task ExitAsync()
    {
        if (exiting) return;
        if (sng is not null && tournamentResult is null)
        {
            if (!await ConfirmAsync(L("Sng.LeaveConfirm", Settings.BuyIn))) return;
            await FinishTournamentAsync(sng.PlayersLeft);
        }
        var handRunning = table.Phase is TablePhase.WaitingForAction or TablePhase.RoundComplete;
        if (sng is null && handRunning && Hero.IsActive && Hero.TotalInvested > 0 && js is not null && !Settings.IsPractice)
        {
            bool ok;
            try
            {
                ok = await JS.InvokeAsync<bool>("confirm",
                    L("Table.LeaveConfirm", Hero.TotalInvested));
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException) { return; }
            if (!ok) return;
        }
        exiting = true;
        cts.Cancel();
        autoCts?.Cancel();
        await OnExit.InvokeAsync(Hero.Chips);
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        if (js is null) return true;
        try { return await JS.InvokeAsync<bool>("confirm", message); }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException) { return false; }
    }

    private async Task ReportAsync(HandOutcome? hand)
    {
        if (Settings.IsPractice || (Settings.IsTournament && hand is null)) return;
        await OnReport.InvokeAsync(new TableReport(Hero.Chips, hand));
    }

    private int BotStack()
    {
        if (Settings.Tournament is { } config) return config.StartingStack;
        if (Settings.IsPractice) return Settings.BuyIn;
        var stake = Settings.Stake;
        var raw = Settings.BuyIn * (0.7 + random.NextDouble() * 0.6);
        var rounded = (int)Math.Round(raw / stake.BigBlind) * stake.BigBlind;
        return Math.Clamp(rounded, stake.MinBuyIn, stake.MaxBuyIn);
    }

    // ------------------------------------------------------------------ hints

    private async Task UpdateEquityAsync()
    {
        if (!hints || !Hero.IsActive || Hero.HoleCards.Count != 2)
        {
            if (!Hero.IsActive) heroOdds = null;
            return;
        }
        var key = (table.HandNumber, table.Board.Count, table.ActivePlayerCount);
        if (key == equityKey && heroOdds is not null) return;
        equityKey = key;
        var hole = Hero.HoleCards.ToArray();
        var board = table.Board.ToArray();
        var opponents = Math.Max(1, table.ActivePlayerCount - 1);
        (heroOdds, startingRank, startingTop) = await Task.Run(() => (
            HandStats.Analyze(hole, board, opponents, 3000),
            (int?)StartingHands.RankOf(hole[0], hole[1]),
            (double?)StartingHands.TopPercent(hole[0], hole[1])));
    }

    private (string Text, string Kind) Tip(double equity)
    {
        // Compare with a fair share of the pot (1 / players still in).
        var opponents = Math.Max(1, table.ActivePlayerCount - 1);
        var strength = equity * (opponents + 1);
        var potOdds = legal is { ToCall: > 0 } l ? l.ToCall / (double)(l.Pot + l.ToCall) : 0;
        if (strength > 1.5) return (L("Tip.Strong"), "strong");
        if (strength > 1.12) return (L(legal?.ToCall > 0 ? "Tip.SolidCall" : "Tip.SolidBet"), "");
        if (potOdds > 0 && equity < potOdds) return (L("Tip.TooExpensive"), "weak");
        return (L(legal?.ToCall > 0 ? "Tip.WeakCall" : "Tip.WeakCheck"), "weak");
    }

    private async Task ToggleHints()
    {
        hints = !hints;
        equityKey = default;
        await UpdateEquityAsync();
    }

    // ------------------------------------------------------------------ raise controls

    private void SetRaise(int value)
    {
        if (legal is null) return;
        if (value < legal.MaxRaiseTo)
            value = (int)Math.Round(value / (double)RaiseUnit) * RaiseUnit;
        raiseTo = Math.Clamp(value, legal.MinRaiseTo, legal.MaxRaiseTo);
    }


    private void StepRaise(int direction) => SetRaise(raiseTo + direction * Math.Max(RaiseUnit, table.BigBlind));


    [JSInvokable]
    public async Task Hotkey(string key)
    {
        await InvokeAsync(async () =>
        {
            if (heroTurn && legal is not null)
            {
                switch (key)
                {
                    case "f": await HeroActAsync(PlayerAction.Fold()); return;
                    case "c": await HeroActAsync(PlayerAction.Call()); return;
                    case "r" when legal.CanRaise: await HeroRaiseAsync(); return;
                    case "a" when legal.CanRaise: SetRaise(legal.MaxRaiseTo); break;
                    case "ArrowUp": StepRaise(1); break;
                    case "ArrowDown": StepRaise(-1); break;
                }
                StateHasChanged();
                return;
            }
            if (key is " " or "Enter" && table.Phase == TablePhase.HandComplete && gameOver is null)
                await NextHandAsync();
        });
    }

    // ------------------------------------------------------------------ layout helpers

    // Where things lie on the felt for each seat slot (percent of the table box).
    private sealed record SlotLayout((double X, double Y) Cards, (double X, double Y) Stack, (double X, double Y) Bet);

    private static readonly SlotLayout[] Layout =
    [
        new((50, 78), (64, 84), (50, 66)),   // bottom (you)
        new((14, 58), (12, 74), (25, 55)),   // left
        new((25, 20), (13, 27), (32, 31)),   // top left
        new((50, 13), (37, 12), (60, 23)),   // top
        new((75, 20), (87, 27), (68, 31)),   // top right
        new((86, 58), (88, 74), (75, 55)),   // right
    ];

    // Deck and discard pile on the felt.
    private const double DeckX = 69, DeckY = 30, MuckX = 78, MuckY = 41;

    private static readonly string[] TrayColors = ["white", "red", "green", "black", "purple", "gold"];

    private (double X, double Y) BetPos(int i) => Layout[slots[i]].Bet;

    private void PauseAutoContinue()
    {
        if (autoPaused || autoContinueMs == 0) return;
        autoCts?.Cancel();
        autoPaused = true;
    }

    private (double X, double Y) DealerPos(int i)
    {
        var (x, y) = SlotPos[slots[i]];
        var (bx, by) = (x + (50 - x) * 0.2, y + (50 - y) * 0.2);
        return slots[i] switch
        {
            0 => (bx + 11, by - 7),
            3 => (bx - 10, by + 6),
            1 or 2 => (bx + 4, by + 9),
            _ => (bx - 4, by + 9),
        };
    }

    /// <summary>Extra label on a seat's plate (used by tournaments for knock-out places).</summary>
    private string? SeatBadge(int seat) =>
        sng?.PlaceOf(seat) is { } place && table.Players[seat].Chips == 0 ? $"{place}." : null;

    private int DealDelay(int seat, int card)
    {
        var n = table.Players.Count;
        var order = ((seat - table.DealerIndex - 1) % n + n) % n;
        return (card * n + order) * 90;
    }

    private static string F(double v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string CardText(Card c) => HandText.RankSymbol(c.Rank) + HandText.SuitSymbol(c.Suit);

    // ------------------------------------------------------------------ JS & misc

    /// <summary>How long the result stays before the next hand (shorter when fast, at least 3 s).</summary>
    private int ResultTime(int ms) => Settings.Speed >= 1 ? ms : Math.Max(3000, (int)(ms * Settings.Speed));

    /// <summary>Waits (shorter with the "fast" and "turbo" speed from the lobby).</summary>
    private Task Delay(int ms) => Task.Delay((int)(ms * Math.Clamp(Settings.Speed, 0.1, 1.5)), cts.Token);

    private void Sfx(string name)
    {
        if (sound) _ = JsAsync("sfx", name);
    }

    private void Bump(string id) => _ = JsAsync("bump", id);

    private async Task JsAsync(string fn, params object?[] args)
    {
        if (js is null) return;
        try { await js.InvokeVoidAsync(fn, args); }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException or ObjectDisposedException) { }
    }

    private async Task ToggleSound()
    {
        sound = !sound;
        await JsAsync("setSound", sound);
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        autoCts?.Cancel();
        if (js is not null)
        {
            try
            {
                await js.InvokeVoidAsync("dispose");
                await js.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or JSException) { }
        }
        selfRef?.Dispose();
    }
}
