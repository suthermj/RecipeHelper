using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.Account
{
    public class LoginVM
    {
        [Required(ErrorMessage = "Enter your email.")]
        [EmailAddress(ErrorMessage = "Enter a valid email.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Enter your password.")]
        public string Password { get; set; } = "";

        public string? ReturnUrl { get; set; }
    }

    // Shared by first-run setup and joining via invite -- both create an account.
    public class CreateAccountVM
    {
        [Required(ErrorMessage = "Enter your name.")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "Enter your email.")]
        [EmailAddress(ErrorMessage = "Enter a valid email.")]
        [MaxLength(256)]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Choose a password.")]
        [MinLength(AccountRules.MinPasswordLength, ErrorMessage = "Use at least 8 characters.")]
        public string Password { get; set; } = "";
    }

    public class SetupVM : CreateAccountVM
    {
        [Required(ErrorMessage = "Name your household.")]
        [MaxLength(100)]
        public string HouseholdName { get; set; } = "";
    }

    public class JoinVM : CreateAccountVM
    {
        public string Token { get; set; } = "";

        // Display only -- re-derived from the token on every request, never trusted
        // from the form.
        public string HouseholdName { get; set; } = "";
        public string InvitedBy { get; set; } = "";
    }

    public class HouseholdMemberVM
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsCurrentUser { get; set; }
    }

    public static class AccountRules
    {
        public const int MinPasswordLength = 8;
        public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);
    }
}
