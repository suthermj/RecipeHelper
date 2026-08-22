namespace RecipeHelper.Models.Settings
{
    public class SettingsVM
    {
        public string CurrentLocationId { get; set; } = "";
        public string CurrentStoreName { get; set; } = "";
        // Only true once AdminSettings:LogsToken is actually configured -- keeps the
        // System Logs button from appearing (and leading to a 404) before the one-time
        // VM setup documented in CLAUDE.md is done.
        public bool LogsAvailable { get; set; }
    }
}
