using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.Account;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.Lists;

namespace RecipeHelper
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);


            builder.Entity<KrogerProduct>()
                .HasKey(p => p.Upc);

            builder.Entity<IngredientKrogerProduct>()
                .HasKey(x => new { x.IngredientId, x.Upc });

            builder.Entity<IngredientKrogerProduct>()
                .HasOne(x => x.Ingredient)
                .WithMany(i => i.KrogerMappings)
                .HasForeignKey(x => x.IngredientId);

            builder.Entity<IngredientKrogerProduct>()
                .HasOne(x => x.KrogerProduct)
                .WithMany(p => p.IngredientMappings)
                .HasForeignKey(x => x.Upc);

            builder.Entity<RecipeIngredient>()
                .HasOne(ri => ri.SelectedKrogerProduct)
                .WithMany(p => p.RecipeIngredients)
                .HasForeignKey(ri => ri.SelectedKrogerUpc)
                .HasPrincipalKey(p => p.Upc)
                .IsRequired(false);

            builder.Entity<MealPlan>()
                .Property(p => p.ShareToken)
                .HasMaxLength(450);

            builder.Entity<MealPlan>()
                .HasIndex(p => p.ShareToken)
                .IsUnique()
                .HasFilter("[ShareToken] IS NOT NULL");

            // RecipeId is optional (free-text entries have no Recipe), but still
            // cascade-delete on the DB side to match the prior required-FK behavior
            // when a Recipe is deleted -- the convention default for an optional FK
            // is ClientSetNull, which this overrides.
            builder.Entity<MealPlanEntry>()
                .HasOne(e => e.Recipe)
                .WithMany()
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();

            builder.Entity<AppUser>()
                .HasOne(u => u.Household)
                .WithMany(h => h.Members)
                .HasForeignKey(u => u.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HouseholdInvite>()
                .HasIndex(i => i.TokenHash)
                .IsUnique();

            builder.Entity<HouseholdInvite>()
                .HasOne(i => i.Household)
                .WithMany()
                .HasForeignKey(i => i.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not cascade): SQL Server rejects a second cascade path from
            // Household to HouseholdInvite (Household -> Users -> invites).
            builder.Entity<HouseholdInvite>()
                .HasOne(i => i.CreatedByUser)
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HouseholdInvite>()
                .HasOne(i => i.UsedByUser)
                .WithMany()
                .HasForeignKey(i => i.UsedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

        }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeProducts { get; set; }
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<KrogerProduct> KrogerProducts => Set<KrogerProduct>();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<IngredientKrogerProduct> IngredientKrogerProducts => Set<IngredientKrogerProduct>();

        public DbSet<Measurement> Measurements { get; set; }
        public DbSet<KrogerCustomerToken> KrogerCustomerTokens { get; set; }
        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }

        public DbSet<MealPlan> MealPlans { get; set; }
        public DbSet<MealPlanEntry> MealPlanEntries { get; set; }

        public DbSet<Household> Households => Set<Household>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<HouseholdInvite> HouseholdInvites => Set<HouseholdInvite>();
    }
}
