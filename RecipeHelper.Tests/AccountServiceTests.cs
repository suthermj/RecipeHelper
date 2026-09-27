using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    public class AccountServiceTests
    {
        private static (AccountService Service, DatabaseContext Db) Create()
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            return (new AccountService(db, NullLogger<AccountService>.Instance), db);
        }

        [Fact]
        public async Task FirstSetup_CreatesHouseholdAndMember_SecondSetupRefused()
        {
            var (svc, db) = Create();

            var owner = await svc.CreateFirstHouseholdAsync("  Home ", "Alex", " Alex@Example.com ", "password123");
            Assert.NotNull(owner);
            Assert.Equal("alex@example.com", owner!.Email);
            Assert.Equal("Home", (await db.Households.SingleAsync()).Name);

            var second = await svc.CreateFirstHouseholdAsync("Other", "Sam", "sam@example.com", "password123");
            Assert.Null(second);
            Assert.Equal(1, await db.Households.CountAsync());
        }

        [Fact]
        public async Task ValidateCredentials_EmailIsCaseInsensitive_WrongPasswordAndUnknownEmailFail()
        {
            var (svc, db) = Create();
            await svc.CreateFirstHouseholdAsync("Home", "Alex", "alex@example.com", "password123");

            Assert.NotNull(await svc.ValidateCredentialsAsync("ALEX@example.com", "password123"));
            Assert.Null(await svc.ValidateCredentialsAsync("alex@example.com", "wrong-password"));
            Assert.Null(await svc.ValidateCredentialsAsync("nobody@example.com", "password123"));

            // Stored as a hash, never the password itself.
            Assert.DoesNotContain("password123", (await db.Users.SingleAsync()).PasswordHash);
        }

        [Fact]
        public async Task Invite_JoinsInvitersHousehold_AndOnlyWorksOnce()
        {
            var (svc, db) = Create();
            var owner = (await svc.CreateFirstHouseholdAsync("Home", "Alex", "alex@example.com", "password123"))!;

            var token = await svc.CreateInviteAsync(owner.HouseholdId, owner.Id);

            var invite = await svc.GetValidInviteAsync(token);
            Assert.NotNull(invite);
            Assert.Equal("Home", invite!.Household.Name);
            Assert.Equal("Alex", invite.CreatedByUser.DisplayName);
            // Only the hash is stored.
            Assert.NotEqual(token, invite.TokenHash);

            var partner = await svc.AcceptInviteAsync(token, "Sam", "sam@example.com", "password456");
            Assert.NotNull(partner);
            Assert.Equal(owner.HouseholdId, partner!.HouseholdId);
            Assert.NotNull(await svc.ValidateCredentialsAsync("sam@example.com", "password456"));

            var stored = await db.HouseholdInvites.SingleAsync();
            Assert.NotNull(stored.UsedUtc);
            Assert.Equal(partner.Id, stored.UsedByUserId);

            // Single use.
            Assert.Null(await svc.GetValidInviteAsync(token));
            Assert.Null(await svc.AcceptInviteAsync(token, "Eve", "eve@example.com", "password789"));

            var (_, members) = await svc.GetHouseholdAsync(owner.HouseholdId);
            Assert.Equal(new[] { "Alex", "Sam" }, members.Select(m => m.DisplayName));
        }

        [Fact]
        public async Task Invite_ExpiredOrUnknownToken_IsInvalid()
        {
            var (svc, db) = Create();
            var owner = (await svc.CreateFirstHouseholdAsync("Home", "Alex", "alex@example.com", "password123"))!;
            var token = await svc.CreateInviteAsync(owner.HouseholdId, owner.Id);

            Assert.Null(await svc.GetValidInviteAsync("not-a-real-token"));
            Assert.Null(await svc.GetValidInviteAsync(""));

            var invite = await db.HouseholdInvites.SingleAsync();
            invite.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();

            Assert.Null(await svc.GetValidInviteAsync(token));
        }
    }
}
