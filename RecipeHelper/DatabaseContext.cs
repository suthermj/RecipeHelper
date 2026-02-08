using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.IngredientModels;
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

        }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<RecipeIngredient> RecipeProducts { get; set; }
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<KrogerProduct> KrogerProducts => Set<KrogerProduct>();
        public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
        public DbSet<IngredientKrogerProduct> IngredientKrogerProducts => Set<IngredientKrogerProduct>();

        public DbSet<Measurement> Measurements { get; set; }
        public DbSet<DraftRecipe> DraftRecipes { get; set; }
        public DbSet<KrogerCustomerToken> KrogerCustomerTokens { get; set; }
        //public DbSet<RecipeHelper.Models.ProductVM> ProductVM { get; set; } = default!;
    }
}
