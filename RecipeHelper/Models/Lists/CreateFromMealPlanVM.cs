namespace RecipeHelper.Models.Lists
{
    public class CreateFromMealPlanVM
    {
        public List<MealPlanItem> Items { get; set; } = new();
    }

    public class MealPlanItem
    {
        public string Name { get; set; } = "";
        public decimal Quantity { get; set; }
        public string? Measurement { get; set; }
        public string? Upc { get; set; }
        public bool Include { get; set; }
    }
}
