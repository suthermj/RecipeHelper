using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;

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
        public DbSet<Product> Products { get; set; }
        public DbSet<RecipeProduct> RecipeProducts { get; set; }
    }
}
