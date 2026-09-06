using System.Security.Cryptography;

namespace RecipeHelper.Utility
{
    // Shared by every "public share link" feature (meal plans, recipes) so token
    // generation stays identical instead of being copy-pasted per entity.
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
