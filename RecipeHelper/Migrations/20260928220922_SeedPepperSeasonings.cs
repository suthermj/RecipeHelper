using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class SeedPepperSeasonings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'red pepper flakes') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('red pepper flakes', 'red pepper flakes')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'crushed red pepper') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('crushed red pepper', 'crushed red pepper')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'cayenne pepper') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cayenne pepper', 'cayenne pepper')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'white pepper') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('white pepper', 'white pepper')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName IN ('red pepper flakes', 'crushed red pepper', 'cayenne pepper', 'white pepper')");
        }
    }
}
