using System.Security.Cryptography;

namespace RecipeHelper.Utility
{
    public static class ShareToken
    {
        // 32 random bytes, base64url-encoded without padding. Used for public read-only share links.
        public static string Generate()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
