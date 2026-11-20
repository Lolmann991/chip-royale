using System.ComponentModel.DataAnnotations;
using ChipRoyale.Engine.Economy;

namespace ChipRoyale.Web.Data;

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

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
