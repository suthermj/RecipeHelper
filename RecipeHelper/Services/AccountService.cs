using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.Account;

namespace RecipeHelper.Services
{
    public class AccountService
    {
        public const string HouseholdIdClaim = "household_id";

        private readonly DatabaseContext _context;
        private readonly ILogger<AccountService> _logger;
        private readonly PasswordHasher<AppUser> _hasher = new();

        // Verified against when the email doesn't match any account, so a wrong
        // email takes as long as a wrong password and response time doesn't reveal
        // which emails have accounts.
        private static readonly string DummyHash = new PasswordHasher<AppUser>()
            .HashPassword(null!, "not-a-real-password");

        public AccountService(DatabaseContext context, ILogger<AccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        public Task<bool> AnyUsersAsync() => _context.Users.AnyAsync();

        public Task<bool> EmailInUseAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            return _context.Users.AnyAsync(u => u.Email == normalized);
        }

        // First-run only: creates the very first household and its first member.
        // Returns null if any account already exists (setup has already happened).
        public async Task<AppUser?> CreateFirstHouseholdAsync(string householdName, string displayName, string email, string password)
        {
            if (await AnyUsersAsync()) return null;

            var now = DateTime.UtcNow;
            var household = new Household { Name = householdName.Trim(), CreatedUtc = now };
            var user = NewUser(household, displayName, email, password, now);

            _context.Households.Add(household);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("First household created. HouseholdId={HouseholdId}, UserId={UserId}", household.Id, user.Id);
            return user;
        }

        public async Task<AppUser?> ValidateCredentialsAsync(string email, string password)
        {
            var normalized = NormalizeEmail(email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);

            if (user == null)
            {
                _hasher.VerifyHashedPassword(null!, DummyHash, password);
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed) return null;

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = _hasher.HashPassword(user, password);

            user.LastLoginUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return user;
        }

        // Returns the raw token for the invite link. Only its hash is stored.
        public async Task<string> CreateInviteAsync(int householdId, int createdByUserId)
        {
            var token = GenerateToken();
            var now = DateTime.UtcNow;

            _context.HouseholdInvites.Add(new HouseholdInvite
            {
                HouseholdId = householdId,
                CreatedByUserId = createdByUserId,
                TokenHash = HashToken(token),
                CreatedUtc = now,
                ExpiresUtc = now + AccountRules.InviteLifetime,
            });
            await _context.SaveChangesAsync();

            _logger.LogInformation("Household invite created. HouseholdId={HouseholdId}, CreatedByUserId={UserId}", householdId, createdByUserId);
            return token;
        }

        // An unused, unexpired invite for this token (with Household and CreatedByUser
        // loaded), or null.
        public async Task<HouseholdInvite?> GetValidInviteAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var hash = HashToken(token);
            var now = DateTime.UtcNow;
            return await _context.HouseholdInvites
                .Include(i => i.Household)
                .Include(i => i.CreatedByUser)
                .FirstOrDefaultAsync(i => i.TokenHash == hash && i.UsedUtc == null && i.ExpiresUtc > now);
        }

        // Creates the account in the invite's household and consumes the invite.
        // Null if the invite is no longer valid.
        public async Task<AppUser?> AcceptInviteAsync(string token, string displayName, string email, string password)
        {
            var invite = await GetValidInviteAsync(token);
            if (invite == null) return null;

            var now = DateTime.UtcNow;
            var user = NewUser(invite.Household, displayName, email, password, now);
            _context.Users.Add(user);
            invite.UsedUtc = now;
            invite.UsedByUser = user;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Household invite accepted. HouseholdId={HouseholdId}, UserId={UserId}", invite.HouseholdId, user.Id);
            return user;
        }

        public async Task<(Household? Household, List<AppUser> Members)> GetHouseholdAsync(int householdId)
        {
            var household = await _context.Households
                .Include(h => h.Members)
                .FirstOrDefaultAsync(h => h.Id == householdId);
            var members = household?.Members.OrderBy(m => m.CreatedUtc).ToList() ?? [];
            return (household, members);
        }

        public static ClaimsPrincipal CreatePrincipal(AppUser user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.DisplayName),
                new(ClaimTypes.Email, user.Email),
                new(HouseholdIdClaim, user.HouseholdId.ToString()),
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        private AppUser NewUser(Household household, string displayName, string email, string password, DateTime now)
        {
            var user = new AppUser
            {
                Household = household,
                Email = NormalizeEmail(email),
                DisplayName = displayName.Trim(),
                PasswordHash = "",
                CreatedUtc = now,
                LastLoginUtc = now,
            };
            user.PasswordHash = _hasher.HashPassword(user, password);
            return user;
        }

        private static string GenerateToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static class ClaimsPrincipalAccountExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal principal) =>
            int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public static int? GetHouseholdId(this ClaimsPrincipal principal) =>
            int.TryParse(principal.FindFirstValue(AccountService.HouseholdIdClaim), out var id) ? id : null;
    }
}
