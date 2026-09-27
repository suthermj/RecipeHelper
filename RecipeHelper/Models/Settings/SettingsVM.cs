using RecipeHelper.Models.Account;

namespace RecipeHelper.Models.Settings
{
    public class SettingsVM
    {
        public string CurrentLocationId { get; set; } = "";
        public string CurrentStoreName { get; set; } = "";

        public string HouseholdName { get; set; } = "";
        public List<HouseholdMemberVM> HouseholdMembers { get; set; } = [];
    }
}
