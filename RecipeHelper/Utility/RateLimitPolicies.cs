namespace RecipeHelper.Utility
{
    public static class RateLimitPolicies
    {
        // Endpoints that spend money or act on the stored Kroger account (OpenAI parsing and
        // photo extraction, Spoonacular import, Kroger cart add). The app has no login yet, so
        // this caps what anyone who finds the URL can run up. Configured in Program.cs.
        public const string PaidApi = "paid-api";
        public const int PerIpPermitsPerMinute = 10;
        public const int GlobalPermitsPerMinute = 30;
    }
}
