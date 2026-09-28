using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class SeedMincedGarlic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'minced garlic') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('minced garlic', 'minced garlic')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'garlic minced') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('garlic minced', 'garlic minced')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'garlic cloves minced') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('garlic cloves minced', 'garlic cloves minced')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName IN ('minced garlic', 'garlic minced', 'garlic cloves minced')");
        }
    }
}
