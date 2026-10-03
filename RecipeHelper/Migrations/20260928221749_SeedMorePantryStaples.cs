using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class SeedMorePantryStaples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'chili powder') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('chili powder', 'chili powder')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'ground ginger') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('ground ginger', 'ground ginger')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'nutmeg') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('nutmeg', 'nutmeg')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'ground cloves') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('ground cloves', 'ground cloves')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'turmeric') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('turmeric', 'turmeric')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'ground coriander') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('ground coriander', 'ground coriander')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'curry powder') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('curry powder', 'curry powder')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'italian seasoning') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('italian seasoning', 'italian seasoning')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'taco seasoning') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('taco seasoning', 'taco seasoning')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'bay leaves') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('bay leaves', 'bay leaves')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'rosemary') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('rosemary', 'rosemary')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'sage') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('sage', 'sage')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'parsley flakes') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('parsley flakes', 'parsley flakes')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'dill weed') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('dill weed', 'dill weed')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'vanilla extract') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('vanilla extract', 'vanilla extract')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'honey') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('honey', 'honey')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'cocoa powder') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cocoa powder', 'cocoa powder')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'yeast') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('yeast', 'yeast')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'cream of tartar') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cream of tartar', 'cream of tartar')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'cooking spray') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cooking spray', 'cooking spray')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'worcestershire sauce') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('worcestershire sauce', 'worcestershire sauce')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'ketchup') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('ketchup', 'ketchup')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'mustard') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('mustard', 'mustard')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'mayonnaise') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('mayonnaise', 'mayonnaise')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'hot sauce') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('hot sauce', 'hot sauce')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'fish sauce') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('fish sauce', 'fish sauce')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName IN ('chili powder', 'ground ginger', 'nutmeg', 'ground cloves', 'turmeric', 'ground coriander', 'curry powder', 'italian seasoning', 'taco seasoning', 'bay leaves', 'rosemary', 'sage', 'parsley flakes', 'dill weed', 'vanilla extract', 'honey', 'cocoa powder', 'yeast', 'cream of tartar', 'cooking spray', 'worcestershire sauce', 'ketchup', 'mustard', 'mayonnaise', 'hot sauce', 'fish sauce')");
        }
    }
}
