using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChipRoyale.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<PlayerProfile> Profiles => Set<PlayerProfile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerProfile>(e =>
        {
            e.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<PlayerProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bankroll lives in the same table (columns Bankroll_Chips, Bankroll_Run, ...).
            e.OwnsOne(p => p.Bankroll, b =>
            {
                b.Property(x => x.Chips);
                b.Property(x => x.TableChips);
                b.Property(x => x.Run);
                b.Property(x => x.RunPeak);
                b.Property(x => x.BestPeak);
                b.Property(x => x.HandsPlayed);
                b.Property(x => x.HandsWon);
                b.Property(x => x.BiggestPot);
                b.Property(x => x.Busts);
                b.Property(x => x.TablesCleared);
                b.Property(x => x.TournamentsPlayed);
                b.Property(x => x.TournamentsWon);
                b.Ignore(x => x.Total);
                b.Ignore(x => x.IsSeated);
                b.Ignore(x => x.IsBroke);
                b.HasIndex(x => x.BestPeak);
            });
            e.Navigation(p => p.Bankroll).IsRequired();
        });
    }
}
