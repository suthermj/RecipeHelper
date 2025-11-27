using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;

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
            builder.Entity<RecipeProduct>()
                .HasOne(r => r.Recipe)
                .WithMany(rp => rp.RecipeProducts)
                .HasForeignKey(rp => rp.RecipeId);
            builder.Entity<RecipeProduct>()
                .HasOne(p => p.Product)
                .WithMany(rp => rp.RecipeProducts)
                .HasForeignKey(rp => rp.ProductId);

        }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<Models.Product> Products { get; set; }
        public DbSet<RecipeProduct> RecipeProducts { get; set; }

        public DbSet<Measurement> Measurements { get; set; }
        public DbSet<DraftRecipe> DraftRecipes { get; set; }
        public DbSet<KrogerCustomerToken> KrogerCustomerTokens { get; set; }
        //public DbSet<RecipeHelper.Models.ProductVM> ProductVM { get; set; } = default!;
    }
}
