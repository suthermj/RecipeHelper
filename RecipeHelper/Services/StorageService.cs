using System.ComponentModel;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using ImageMagick;
using RecipeHelper.Models;

namespace RecipeHelper.Services
{
    public class StorageService
    {
        // Recipe image blobs are effectively immutable per URL (see StoreRecipeImage's
        // filename generation), so a long-lived, immutable Cache-Control is safe — same
        // value already used for fingerprinted static assets in Program.cs.
        public const string RecipeImageCacheControl = "public, max-age=31536000, immutable";

        // Mirrors IngredientsService.CompressStandardImage's resize/recompress approach.
        private const int MaxImageDimension = 2000;
        private const int JpegQuality = 88;

        private BlobContainerClient _blobContainerClient;
        private BlobClient _blobClient;
        private BlobServiceClient _blobServiceClient;
        private ILogger<StorageService> _logger;
        private readonly string _accountUri;

        // Lets callers (e.g. ImportService) check whether a given ImageUri is already
        // one of our own blobs vs. an external recipe-source URL that needs re-hosting.
        public string AccountUri => _accountUri;

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
            await using var stream = image.OpenReadStream();
            return await StoreRecipeImage(stream, image.FileName, image.ContentType);
        }

        public async Task<StoreImageBlobResponse> StoreRecipeImage(Stream imageStream, string originalFileName, string contentType)
        {
            StoreImageBlobResponse response = new StoreImageBlobResponse();
            Random rand = new Random();
            int guid = rand.Next(100);

            try
            {
                using var originalBuffer = new MemoryStream();
                await imageStream.CopyToAsync(originalBuffer);

                var (uploadBytes, uploadFileName, uploadContentType) = CompressRecipeImage(
                    originalBuffer.ToArray(), originalFileName, contentType);
                string fileName = uploadFileName.Replace(" ", ",") + guid.ToString();

                // Create a blob container if it doesn't exist
                var containerClient = _blobServiceClient.GetBlobContainerClient("recipe-images");
                var blobClient = containerClient.GetBlobClient(fileName);
                using var uploadStream = new MemoryStream(uploadBytes);

                await blobClient.UploadAsync(uploadStream, new BlobHttpHeaders
                {
                    ContentType = uploadContentType,
                    CacheControl = RecipeImageCacheControl
                });

                response.BlobUri = $"{_accountUri}/recipe-images/{fileName}";
                response.BlobName = fileName;
                return response;
            }
            catch (Exception ex)
            {
                // Covers both the upload itself and CompressRecipeImage's own resize/recompress
                // step (which can throw for reasons unrelated to a bad image — e.g. a Magick.NET
                // native-library issue specific to the host) — callers already have to handle a
                // null return here, so let this be the single place that can fail.
                // Exception type/message are put directly in the message template (not just
                // passed as the LogError exception argument) because the OTel exporter records
                // those on separate exception.* attributes that haven't been showing up in
                // Grafana Cloud log exports/downloads — this way the answer is in the line
                // itself regardless of how it's viewed.
                _logger.LogError(ex, "Failed to store recipe image. FileName={FileName}, ContentType={ContentType}, ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    originalFileName, contentType, ex.GetType().FullName, ex.Message);
                return null;
            }
        }

        // Resizes/recompresses a recipe image before upload, mirroring
        // IngredientsService.CompressStandardImage's approach for photo import.
        //
        // Deliberately catches everything, not just the three exception types Magick
        // itself documents (MagickException/NotSupportedException/InvalidOperationException).
        // Compression is a best-effort optimization on top of a feature (uploading an
        // image) that worked before it existed — it must never be able to sacrifice the
        // whole upload. A narrower catch here previously let an unexpected exception type
        // (e.g. a native-library load failure specific to the production host, which
        // surfaces as something other than those three types) fall through uncaught to
        // StoreRecipeImage's outer catch, silently dropping the image entirely instead of
        // just skipping the resize/recompress step.
        private (byte[] Bytes, string FileName, string ContentType) CompressRecipeImage(
            byte[] originalBytes, string originalFileName, string originalContentType)
        {
            try
            {
                using var input = new MemoryStream(originalBytes);
                using var image = new MagickImage(input);

                image.AutoOrient();
                image.Strip();
                image.Format = MagickFormat.Jpeg;
                image.Quality = JpegQuality;

                if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
                {
                    image.Resize(new MagickGeometry(MaxImageDimension, MaxImageDimension)
                    {
                        IgnoreAspectRatio = false
                    });
                }

                using var output = new MemoryStream();
                image.Write(output);

                var convertedFileName = Path.ChangeExtension(originalFileName, ".jpg") ?? originalFileName;
                _logger.LogInformation(
                    "Recipe image compressed. FileName={FileName}, SourceLengthBytes={SourceLengthBytes}, CompressedLengthBytes={CompressedLengthBytes}, Width={Width}, Height={Height}",
                    originalFileName, originalBytes.Length, output.Length, image.Width, image.Height);

                return (output.ToArray(), convertedFileName, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Recipe image compression failed, uploading original uncompressed. FileName={FileName}, ContentType={ContentType}, ExceptionType={ExceptionType}",
                    originalFileName, originalContentType, ex.GetType().FullName);
                return (originalBytes, originalFileName, originalContentType);
            }
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
