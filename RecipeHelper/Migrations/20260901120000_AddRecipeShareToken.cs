using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeShareToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Recipes",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_ShareToken",
                table: "Recipes",
                column: "ShareToken",
                unique: true,
                filter: "[ShareToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recipes_ShareToken",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Recipes");
        }
    }
}
