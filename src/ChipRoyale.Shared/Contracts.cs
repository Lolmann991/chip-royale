using ChipRoyale.Engine.Economy;
using ChipRoyale.Engine.Evaluation;

namespace ChipRoyale.Shared;

/// <summary>Routes of the game API (the server maps them, the client calls them).</summary>
public static class ApiRoutes
{
    public const string Health = "api/health";
    public const string Auth = "api/auth";
    public const string Profile = "api/profile";
    public const string Rename = "api/profile/rename";
    public const string BuyIn = "api/bankroll/buyin";
    public const string Table = "api/bankroll/table";
    public const string CashOut = "api/bankroll/cashout";
    public const string TournamentEnter = "api/tournament/enter";
    public const string TournamentFinish = "api/tournament/finish";
    public const string Hand = "api/hand";
    public const string Exchange = "api/shop/exchange";
    public const string Buy = "api/shop/buy";
    public const string Equip = "api/shop/equip";
    public const string Lessons = "api/lessons/complete";
    public const string Leaderboard = "api/leaderboard";
    public const string Dev = "api/dev";
}

/// <summary>Everything the client needs to know about a player.</summary>
public sealed record ProfileDto(
    string DisplayName,
    BankrollState Bankroll,
    string OwnedItems,
    string EquippedItems,
    string Achievements)
{
    public PlayerCollection ToCollection() => PlayerCollection.Parse(OwnedItems, EquippedItems, Achievements);

    public static ProfileDto From(string name, Bankroll bankroll, PlayerCollection collection) => new(
        name, bankroll.ToState(), collection.SerializeOwned(), collection.SerializeEquipped(), collection.SerializeAchievements());
}

/// <summary>Answer to every profile call: the new state plus what just happened.</summary>
/// <param name="Unlocked">Achievement ids unlocked by this call.</param>
/// <param name="Error">Error key (resource key) if the request was refused.</param>
public sealed record ProfileResponse(
    ProfileDto Profile,
    string[] Unlocked,
    bool StartedNewRun = false,
    int ReturnedFromTable = 0,
    string? Error = null);

public sealed record HandOutcomeDto(bool Won, int Pot, bool TableCleared, HandCategory? WinningHand)
{
    public GameEvent ToEvent() => new() { HandWon = Won, Pot = Pot, WinningHand = WinningHand, TableCleared = TableCleared };
}

public sealed record BuyInRequest(string StakeId, int Amount);
public sealed record TableRequest(int Stack, HandOutcomeDto? Hand);
public sealed record CashOutRequest(int Stack);
public sealed record TournamentEnterRequest(string TierId);
public sealed record TournamentFinishRequest(string TierId, int Place);
public sealed record ExchangeRequest(int Chips);
public sealed record ItemRequest(string ItemId);
public sealed record RenameRequest(string Name);

public sealed record LeaderboardEntry(
    int Rank,
    string Name,
    int BestPeak,
    int Total,
    int Run,
    int HandsPlayed,
    int HandsWon,
    int TablesCleared,
    int TournamentsPlayed,
    int TournamentsWon,
    bool IsMe,
    bool DevTools = false);

public sealed record LeaderboardResponse(IReadOnlyList<LeaderboardEntry> Top, LeaderboardEntry? Me);

/// <summary>Rules for display names (checked on the server and offline on the client).</summary>
public static class NameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    /// <returns>A resource key describing the problem, or null if the name is fine.</returns>
    public static string? Check(string name)
    {
        name = name.Trim();
        if (name.Length is < MinLength or > MaxLength) return "Name.Length";
        if (!name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '_' or '-')) return "Name.Chars";
        return null;
    }
}
