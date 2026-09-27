using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.Account
{
    // The unit that will own data (recipes, meal plans, shopping lists) once data is
    // scoped -- see MULTI_USER_ROADMAP.md. Everyone in a household sees and edits the
    // same things; a household is joined by invite link, never by self-signup into an
    // existing one.
    public class Household
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public required string Name { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<AppUser> Members { get; set; } = [];
    }

    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household Household { get; set; } = null!;

        // Stored trimmed + lowercased (AccountService.NormalizeEmail) and unique, so
        // lookups are a plain equality match.
        [MaxLength(256)]
        public required string Email { get; set; }

        [MaxLength(100)]
        public required string DisplayName { get; set; }

        // ASP.NET Core Identity's PasswordHasher format (PBKDF2, versioned) -- the
        // hasher is used on its own, without the rest of Identity.
        public required string PasswordHash { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }

    // Single-use, expiring invite link into a household. Only a SHA-256 hash of the
    // token is stored, so the table itself can't be used to mint a working link.
    public class HouseholdInvite
    {
        [Key]
        public int Id { get; set; }

        public int HouseholdId { get; set; }
        public Household Household { get; set; } = null!;

        [MaxLength(64)]
        public required string TokenHash { get; set; }

        public int CreatedByUserId { get; set; }
        public AppUser CreatedByUser { get; set; } = null!;

        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }

        public DateTime? UsedUtc { get; set; }
        public int? UsedByUserId { get; set; }
        public AppUser? UsedByUser { get; set; }
    }
}
