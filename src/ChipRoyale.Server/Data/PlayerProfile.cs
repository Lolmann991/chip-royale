using System.ComponentModel.DataAnnotations;
using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Server.Data;

/// <summary>Game data of a registered player: display name and bankroll (chips, run, records).</summary>
public class PlayerProfile
{
    public const int MaxNameLength = 20;

    [Key]
    public string UserId { get; set; } = "";

    public ApplicationUser? User { get; set; }

    [MaxLength(MaxNameLength)]
    public string DisplayName { get; set; } = "";

    public Bankroll Bankroll { get; set; } = Bankroll.New();

    /// <summary>Bought shop items (comma-separated ids).</summary>
    public string OwnedItems { get; set; } = "";

    /// <summary>Worn items per category ("Felt=felt-ocean;Avatar=av-robot").</summary>
    public string EquippedItems { get; set; } = "";

    /// <summary>Unlocked achievement ids (comma-separated).</summary>
    public string UnlockedAchievements { get; set; } = "";

    /// <summary>Roulette, slots, video poker statistics and the daily bonus (JSON, see <see cref="Engine.Economy.CasinoStats"/>).</summary>
    public string CasinoStats { get; set; } = "";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PlayerCollection LoadCollection() => PlayerCollection.Parse(OwnedItems, EquippedItems, UnlockedAchievements);

    public void StoreCollection(PlayerCollection c)
    {
        OwnedItems = c.SerializeOwned();
        EquippedItems = c.SerializeEquipped();
        UnlockedAchievements = c.SerializeAchievements();
    }
}
