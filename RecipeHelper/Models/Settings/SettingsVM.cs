namespace RecipeHelper.Models.Settings
{
    public class SettingsVM
    {
        public string CurrentLocationId { get; set; } = "";
        public string CurrentStoreName { get; set; } = "";
        public List<RecipeHelper.Models.PantryItem> PantryItems { get; set; } = new();
    }
}
