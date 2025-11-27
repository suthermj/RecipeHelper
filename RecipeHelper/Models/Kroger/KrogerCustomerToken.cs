using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.Kroger
{
    public class KrogerCustomerToken
    {
        [Key]
        public int Id { get; set; }

        // This is the "id" from /identity/profile
        public string KrogerProfileId { get; set; } = null!;

        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    }
}