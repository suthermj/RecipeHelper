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
            var storageSettings = configuration.GetSection("StorageSettings");
            _accountUri = storageSettings["accountUri"];
            _blobServiceClient = new BlobServiceClient(storageSettings["connectionString"]);
            _blobContainerClient = new BlobContainerClient(storageSettings["connectionString"], "recipe-images");
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
                    var result = await blobClient.UploadAsync(stream);
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

    }
}
