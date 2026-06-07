using System.ComponentModel;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using RecipeHelper.Models;

namespace RecipeHelper.Services
{
    public class StorageService
    {
        private BlobContainerClient _blobContainerClient;
        private BlobClient _blobClient;
        private BlobServiceClient _blobServiceClient;
        private ILogger<StorageService> _logger;
        private readonly string _accountUri;

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
        {
            _logger = logger;
            var storageSettings = configuration.GetSection("StorageSettings");
            _accountUri = storageSettings["accountUri"] ?? throw new InvalidOperationException("StorageSettings:accountUri not configured");

            var connectionString = storageSettings["connectionString"];
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                // Local dev: storage account key connection string
                _blobServiceClient = new BlobServiceClient(connectionString);
                _blobContainerClient = new BlobContainerClient(connectionString, "recipe-images");
            }
            else
            {
                // Production: Entra service principal
                var tenantId = configuration["AzureAd:TenantId"] ?? throw new InvalidOperationException("AzureAd:TenantId not configured");
                var clientId = configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
                var clientSecret = configuration["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("AzureAd:ClientSecret not configured");
                var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                _blobServiceClient = new BlobServiceClient(new Uri(_accountUri), credential);
                _blobContainerClient = new BlobContainerClient(new Uri($"{_accountUri}/recipe-images"), credential);
            }
        }

        public async Task<StoreImageBlobResponse> StoreRecipeImage(IFormFile image)
        {
            StoreImageBlobResponse response = new StoreImageBlobResponse();
            Random rand = new Random();
            int guid = rand.Next(100);
            string fileName = image.FileName.Replace(" ", ",") + guid.ToString();

            try
            {
                // Create a blob container if it doesn't exist
                var containerClient = _blobServiceClient.GetBlobContainerClient("recipe-images");
                var blobClient = containerClient.GetBlobClient(fileName);
                using (Stream stream = image.OpenReadStream())
                {
                    var result = await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = image.ContentType });
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
                return null;
            }

            string fileUri = $"{_accountUri}/recipe-images/{fileName}";
            response.BlobUri = fileUri;
            response.BlobName = fileName;
            return response;

        }

        public async Task<bool> DeleteImageRecipe(string fileName)
        {
            try
            {
                _logger.LogInformation("Deleting recipe image blob [{fileName}]", fileName);
                // Create a blob container if it doesn't exist
                var containerClient = _blobServiceClient.GetBlobContainerClient("recipe-images");
                var blobClient = containerClient.GetBlobClient(fileName);
                await blobClient.DeleteAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
        }

    }
}
