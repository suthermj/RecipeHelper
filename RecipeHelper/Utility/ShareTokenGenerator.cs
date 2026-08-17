using System.Security.Cryptography;

namespace RecipeHelper.Utility
{
    // Generates the random, unguessable tokens used for public read-only share links
    // (meal plans, recipes). Shared so every share-able entity produces tokens the
    // same way instead of each service re-implementing it.
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
