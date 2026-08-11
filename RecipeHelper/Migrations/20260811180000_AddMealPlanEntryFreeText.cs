using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanEntryFreeText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanEntries_Recipes_RecipeId",
                table: "MealPlanEntries");

            migrationBuilder.AlterColumn<int>(
                name: "RecipeId",
                table: "MealPlanEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "FreeText",
                table: "MealPlanEntries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanEntries_Recipes_RecipeId",
                table: "MealPlanEntries",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealPlanEntries_Recipes_RecipeId",
                table: "MealPlanEntries");

            migrationBuilder.DropColumn(
                name: "FreeText",
                table: "MealPlanEntries");

            migrationBuilder.AlterColumn<int>(
                name: "RecipeId",
                table: "MealPlanEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int?),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MealPlanEntries_Recipes_RecipeId",
                table: "MealPlanEntries",
                column: "RecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
