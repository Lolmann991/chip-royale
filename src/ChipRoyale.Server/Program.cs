using System.Linq.Expressions;
using System.Security.Claims;
using ChipRoyale.Server.Data;
using ChipRoyale.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- database & accounts
var (connectionString, dataDirectory) = PlayerDataLocation.Resolve(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? "DataSource=app.db;Cache=Shared",
    builder.Configuration["Storage:DataDirectory"],
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        // Same schema as the database created by earlier versions (includes the passkey table).
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        // A game account: 6+ characters, no special-character rules.
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Login tokens stay valid across restarts.
builder.Services.AddDataProtection()
    .SetApplicationName("ChipRoyale")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));

// ---------------------------------------------------------------- CORS (game on GitHub Pages → this server)
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .SetIsOriginAllowed(origin =>
        allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)
        || Uri.TryCreate(origin, UriKind.Absolute, out var uri) && (uri.IsLoopback || uri.Host == "localhost"))
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}
app.Logger.LogInformation("Chip Royale database: {Path}", connectionString);

// Chrome asks before a public site (GitHub Pages) may talk to a server on this computer.
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
        context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
    await next();
});
app.UseCors();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------- API
app.MapGet(ApiRoutes.Health, () => Results.Ok(new { ok = true, game = "Chip Royale" }));
app.MapGroup(ApiRoutes.Auth).MapIdentityApi<ApplicationUser>();

var api = app.MapGroup("/").RequireAuthorization();
api.MapGet(ApiRoutes.Profile, (ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, ProfileOperations.Load));
api.MapPost(ApiRoutes.Rename, async (RenameRequest request, ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var id = UserId(user);
    var name = request.Name.Trim();
    var lower = name.ToLower();
    if (await db.Profiles.AnyAsync(p => p.UserId != id && p.DisplayName.ToLower() == lower))
        return await Mutate(user, db, _ => OperationResult.Fail("Name.Taken"));
    return await Mutate(user, db, s => ProfileOperations.Rename(s, name));
});
api.MapPost(ApiRoutes.BuyIn, (BuyInRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.BuyIn(s, r.StakeId, r.Amount)));
api.MapPost(ApiRoutes.Table, (TableRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.Table(s, r.Stack, r.Hand)));
api.MapPost(ApiRoutes.CashOut, (CashOutRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.CashOut(s, r.Stack)));
api.MapPost(ApiRoutes.TournamentEnter, (TournamentEnterRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.TournamentEnter(s, r.TierId)));
api.MapPost(ApiRoutes.TournamentFinish, (TournamentFinishRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.TournamentFinish(s, r.TierId, r.Place)));
api.MapPost(ApiRoutes.Hand, (HandOutcomeDto r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.Hand(s, r)));
api.MapPost(ApiRoutes.Exchange, (ExchangeRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.Exchange(s, r.Chips)));
api.MapPost(ApiRoutes.Buy, (ItemRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.Buy(s, r.ItemId)));
api.MapPost(ApiRoutes.Equip, (ItemRequest r, ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, s => ProfileOperations.Equip(s, r.ItemId)));
api.MapPost(ApiRoutes.Lessons, (ClaimsPrincipal user, ApplicationDbContext db) =>
    Mutate(user, db, ProfileOperations.LessonsCompleted));

app.MapGet(ApiRoutes.Leaderboard, async (string? sort, ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var id = user.Identity?.IsAuthenticated == true ? UserId(user) : null;
    Expression<Func<PlayerProfile, int>> key = sort switch
    {
        "aktuell" => p => p.Bankroll.Chips + p.Bankroll.TableChips,
        "tische" => p => p.Bankroll.TablesCleared,
        "turniere" => p => p.Bankroll.TournamentsWon,
        _ => p => p.Bankroll.BestPeak,
    };
    var value = key.Compile();
    var top = await db.Profiles.AsNoTracking().OrderByDescending(key).ThenByDescending(p => p.Bankroll.HandsWon).Take(50).ToListAsync();
    var rows = top.Select((p, i) => Entry(p, i + 1, p.UserId == id)).ToList();

    LeaderboardEntry? me = rows.FirstOrDefault(r => r.IsMe);
    if (me is null && id is not null && await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == id) is { } mine)
    {
        var myValue = value(mine);
        var better = await db.Profiles.Select(key).CountAsync(v => v > myValue);
        me = Entry(mine, better + 1, true);
    }
    return Results.Ok(new LeaderboardResponse(rows, me));
});

app.MapFallbackToFile("index.html");
app.Run();

// ---------------------------------------------------------------- helpers

static string UserId(ClaimsPrincipal user) =>
    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No user id.");

static async Task<IResult> Mutate(ClaimsPrincipal user, ApplicationDbContext db, Func<PlayerState, OperationResult> operation)
{
    var id = UserId(user);
    var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == id);
    if (profile is null)
    {
        profile = new PlayerProfile { UserId = id, DisplayName = $"Spieler{Random.Shared.Next(1000, 10000)}" };
        db.Profiles.Add(profile);
    }

    var state = new PlayerState(profile.DisplayName, profile.Bankroll, profile.LoadCollection());
    var result = operation(state);
    profile.DisplayName = state.DisplayName;
    profile.StoreCollection(state.Collection);
    profile.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(result.ToResponse(state));
}

static LeaderboardEntry Entry(PlayerProfile p, int rank, bool isMe)
{
    var b = p.Bankroll;
    return new LeaderboardEntry(rank, p.DisplayName, b.BestPeak, b.Total, b.Run, b.HandsPlayed, b.HandsWon,
        b.TablesCleared, b.TournamentsPlayed, b.TournamentsWon, isMe);
}

/// <summary>Marker for integration tests (WebApplicationFactory).</summary>
public partial class Program;
