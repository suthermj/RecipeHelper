using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanShareToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "MealPlans",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealPlans_ShareToken",
                table: "MealPlans",
                column: "ShareToken",
                unique: true,
                filter: "[ShareToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MealPlans_ShareToken",
                table: "MealPlans");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "MealPlans");
        }
    }
}
