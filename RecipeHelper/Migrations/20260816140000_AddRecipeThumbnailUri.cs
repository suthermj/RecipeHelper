using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeThumbnailUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUri",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailUri",
                table: "Recipes");
        }
    }
}
