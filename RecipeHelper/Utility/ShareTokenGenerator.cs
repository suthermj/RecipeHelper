using System.Security.Cryptography;

namespace RecipeHelper.Utility
{
    // Shared by MealPlanService and RecipeService (both expose a "public share link"
    // feature backed by a random, unguessable token column) so the generation scheme
    // stays identical instead of being copy-pasted per entity.
    public static class ShareTokenGenerator
    {
        public static string Generate()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
