using Azure.Core;
using Azure.Identity;

namespace RecipeHelper.Utility
{
    public static class AzureCredentials
    {
        // One credential for every Azure call (Key Vault, blob storage, App Insights).
        // With AzureAd:ClientSecret configured it's the sp-recipe-helper-p service principal
        // (local dev, and prod before Arc). Without it, it's the machine's managed identity --
        // on the Hetzner VM that comes from the Azure Arc agent, so no secret lives on disk.
        // See deploy/arc-keyvault.md.
        public static TokenCredential Create(IConfiguration configuration)
        {
            var clientSecret = configuration["AzureAd:ClientSecret"];
            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                return new ClientSecretCredential(
                    configuration["AzureAd:TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId not configured"),
                    configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured"),
                    clientSecret);
            }

            return new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
        }
    }
}
