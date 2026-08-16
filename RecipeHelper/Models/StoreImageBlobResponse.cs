namespace RecipeHelper.Models
{
    public class StoreImageBlobResponse
    {
        public string BlobName { get; set; }
        public string BlobUri { get; set; }

        // Null if thumbnail generation/upload failed -- callers fall back to the
        // full-size BlobUri in that case rather than failing the whole upload.
        public string ThumbnailName { get; set; }
        public string ThumbnailUri { get; set; }
    }
}
