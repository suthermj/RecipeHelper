
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ImageMagick;
using OpenAI;
using OpenAI.Chat;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Services
{
    
    public class IngredientsService
    {
        private const long StandardPhotoMaxBytes = 15L * 1024L * 1024L;
        private const long DngPhotoMaxBytes = 120L * 1024L * 1024L;

        private DatabaseContext _context;
        private readonly OpenAIClient _oaiClient;
        private ILogger<IngredientsService> _logger;
        private ChatClient _chatClient;

        public IngredientsService(DatabaseContext context, ILogger<IngredientsService> logger, OpenAIClient openAIClient)
        {
            _context = context;
            _logger = logger;
            _oaiClient = openAIClient;
            _chatClient = _oaiClient.GetChatClient("gpt-4o-mini");
        }

        public async Task<int> CanonicalizedIngredientExists (string canonicalizedName)
        {
            if (string.IsNullOrWhiteSpace(canonicalizedName)) return 0;

            canonicalizedName = Normalize(canonicalizedName);

            return await _context.Ingredients
                .Where(i => i.CanonicalName == canonicalizedName)
                .Select(i => i.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<Ingredient> GetIngredientByCanonical(string canonicalizedName)
        {
            if (string.IsNullOrWhiteSpace(canonicalizedName)) return null;

            canonicalizedName = Normalize(canonicalizedName);

            return await _context.Ingredients
                .Where(i => i.CanonicalName == canonicalizedName)
                .FirstOrDefaultAsync();
        }

        public async Task<List<IngredientKrogerProduct>> GetLinkedKrogerProductsAsync(int ingredientId)
        {
            var ingredient = await _context.Ingredients.FindAsync(ingredientId);

            if (ingredient is null) return null;

            return ingredient.KrogerMappings;
        }

        public async Task<bool> IngredientProductLinkExists(int ingredientId, string upc)
        {
            var ingredient = await _context.IngredientKrogerProducts.Where(i => i.IngredientId == ingredientId && i.Upc == upc).FirstOrDefaultAsync();

            if (ingredient is null) return false;

            return true;
        }

        public async Task<CanonicalizeResult> CanonicalizeAsync(string userText, CancellationToken ct)
        {
            userText = (userText ?? "").Trim();
            if (userText.Length == 0) throw new ArgumentException("Empty ingredient name");

            // Very light cleanup (not “stripping”, just normalizing whitespace)
            var normalized = Regex.Replace(userText, @"\s+", " ").Trim();

            var prompt = $@"
                You normalize recipe ingredients.

                Return ONLY valid JSON in this exact shape:
                {{
                  ""canonical_name"": ""string"",
                  ""confidence"": number
                }}

                Rules:
                - lowercase
                - singular
                - ignore preparation words
                - ignore quantities and units

                Canonical name should match the grocery item someone would buy.

                Keep distinct forms when they are different products, including:
                - tomato paste vs canned tomatoes vs tomato sauce
                - chicken broth vs chicken stock vs bouillon
                - butter vs margarine
                - flour vs bread flour vs almond flour
                - cheddar cheese vs parmesan cheese

                Ingredient: ""{normalized}""
                ";


            List<ChatMessage> messages =
            [
                ChatMessage.CreateSystemMessage("You are a strict JSON generator."),
                ChatMessage.CreateUserMessage(prompt),
            ];

            var completion = await _chatClient.CompleteChatAsync(messages,
                new ChatCompletionOptions
                {
                    Temperature = 0
                },
                ct
            );

            var text = StripMarkdownFences(completion.Value.Content[0].Text);

            var result = JsonSerializer.Deserialize<CanonicalizeResult>(text)
                         ?? throw new InvalidOperationException("Invalid JSON from OpenAI");
            result.CanonicalName = Normalize(result.CanonicalName);

            return result;
        }

        private static string StripMarkdownFences(string text)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("```"))
            {
                // Remove opening fence (e.g. ```json)
                var firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0)
                    text = text[(firstNewline + 1)..];

                // Remove closing fence
                if (text.EndsWith("```"))
                    text = text[..^3];
            }
            return text.Trim();
        }

        private static string Normalize(string s){
            return string.Join(" ",
            (s ?? "")
                .ToLowerInvariant()
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }


        public async Task<IngredientParseResponse> TransformRawIngredients(List<string> rawLines, CancellationToken ct)
        {
            var inputJson = JsonSerializer.Serialize(rawLines);

            var prompt = $@"
                Convert these ingredient lines to JSON.
                Rules:

                quantity must be a decimal number (e.g. 1.5). if no quantity is provided in the raw ingredient line, you can assume the quantity is 1
                unit must be one of: tsp, tbsp, cup, oz, lb, ml, l, unit, pt. if no unit is provided in the raw ingredient line, you can assume the unit is 'unit'
                originalQuantity must be the quantity as displayed in the raw text
                ingredientName Will be displayed as the recipe ingredient in the UI. Remove the quantity and unit from the raw ingredient line
                canonicalName should be lowercase, trimmed, remove brand/descriptors, singular where reasonable. Canonical name should match the grocery item someone would buy
                Also for the canonicalName, eep distinct forms when they are different products, including:
                - tomato paste vs canned tomatoes vs tomato sauce
                - chicken broth vs chicken stock vs bouillon
                - butter vs margarine
                - flour vs bread flour vs almond flour
                - cheddar cheese vs parmesan cheese

                Remove “optional” ingredients. Do no include ingredients that are obviously not ingredients.
                Return ONLY JSON, no markdown.
                Output must be: {{ ""items"": [ ... ] }}
                Each item must include: quantity (decimal|0), unit (string|'unit'), originalQuantity (string|'1'), name (string), canonicalName (string).
                Keep the same order as input.

                Input:
                {inputJson}
                ";


            List<ChatMessage> messages =
            [
                ChatMessage.CreateSystemMessage("You are a strict JSON generator. You extract structured ingredient info from raw recipe ingredient lines."),
                ChatMessage.CreateUserMessage(prompt),
            ];

            var completion = await _chatClient.CompleteChatAsync(messages,
                new ChatCompletionOptions
                {
                    Temperature = 0
                },
                ct
            );

            var text = StripMarkdownFences(completion.Value.Content[0].Text);

            var result = JsonSerializer.Deserialize<IngredientParseResponse>(text)
                         ?? throw new InvalidOperationException("Invalid JSON from OpenAI");
         
            return result;
        }

        private const string PhotoExtractionPrompt = """
            You are a recipe extraction assistant. Extract the complete recipe from the provided image(s) of a cookbook, recipe card, or printed recipe.

            Return ONLY a JSON object — no markdown, no explanation — with exactly these fields:
            {
              "title": "Recipe name as printed",
              "ingredients": [
                { "quantity": 1.5, "unit": "cup", "name": "all-purpose flour" }
              ],
              "steps": [
                "Preheat the oven to 350°F.",
                "Mix the dry ingredients in a bowl."
              ],
              "coverImage": {
                "sourceImageIndex": 1,
                "x": 0.12,
                "y": 0.08,
                "width": 0.76,
                "height": 0.32,
                "reason": "Photo of the finished dish"
              }
            }

            Rules:
            - quantity: decimal number or null (convert fractions: 1/2 → 0.5, 3/4 → 0.75, 1/3 → 0.33)
            - unit: use one of: tsp, tbsp, cup, oz, fl oz, lb, g, ml, l, pt, qt — or null for unitless items (e.g. 2 eggs)
            - name: clean display name with no quantity or unit prefix
            - steps: complete cooking instructions in order; each instruction is one string
            - If multiple images are provided, treat them as pages of the same recipe
            - coverImage: if a clear photo or illustration of the finished recipe appears anywhere in the uploaded images, return a crop box for it
            - coverImage.sourceImageIndex is 1-based, matching the order images were uploaded
            - coverImage x, y, width, and height are normalized decimal coordinates from 0 to 1 relative to the full source image
            - The coverImage box should tightly contain the food/recipe image, not the surrounding text, page margins, or ingredient list
            - If no usable recipe image is visible, set coverImage to null
            - Return only valid JSON — no markdown code fences, no extra text
            """;

        private sealed record PhotoExtractionResult(
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("ingredients")] List<RawExtractedIngredient> Ingredients,
            [property: JsonPropertyName("steps")] List<string> Steps,
            [property: JsonPropertyName("coverImage")] CoverImageCandidate? CoverImage);

        private sealed record CoverImageCandidate(
            [property: JsonPropertyName("sourceImageIndex")] int? SourceImageIndex,
            [property: JsonPropertyName("x")] decimal? X,
            [property: JsonPropertyName("y")] decimal? Y,
            [property: JsonPropertyName("width")] decimal? Width,
            [property: JsonPropertyName("height")] decimal? Height,
            [property: JsonPropertyName("reason")] string? Reason);

        private sealed record RawExtractedIngredient(
            [property: JsonPropertyName("quantity")] decimal? Quantity,
            [property: JsonPropertyName("unit")] string? Unit,
            [property: JsonPropertyName("name")] string Name);

        public sealed record NormalizedRecipePhoto(
            string OriginalFileName,
            string FileName,
            string MimeType,
            byte[] Bytes,
            bool WasConverted,
            string? PostedContentType,
            long SourceLength);

        public sealed record RecipeCoverCrop(
            int SourceImageIndex,
            decimal X,
            decimal Y,
            decimal Width,
            decimal Height,
            string? Reason);

        private static string? GetAllowedImageMimeType(IFormFile photo)
        {
            Span<byte> header = stackalloc byte[12];
            using var stream = photo.OpenReadStream();
            var bytesRead = stream.Read(header);

            if (bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return "image/png";
            }

            if (bytesRead >= 12 &&
                header[0] == 0x52 &&
                header[1] == 0x49 &&
                header[2] == 0x46 &&
                header[3] == 0x46 &&
                header[8] == 0x57 &&
                header[9] == 0x45 &&
                header[10] == 0x42 &&
                header[11] == 0x50)
            {
                return "image/webp";
            }

            return null;
        }

        private static bool IsDngImage(IFormFile photo)
        {
            var fileName = photo.FileName ?? string.Empty;
            if (fileName.EndsWith(".dng", StringComparison.OrdinalIgnoreCase))
                return true;

            var contentType = photo.ContentType ?? string.Empty;
            if (contentType.Contains("dng", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private NormalizedRecipePhoto ConvertDngToJpeg(IFormFile photo, int index)
        {
            var conversionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var input = photo.OpenReadStream();
                using var image = new MagickImage(input);

                image.AutoOrient();
                image.Strip();
                image.Format = MagickFormat.Jpeg;
                image.Quality = 88;

                if (image.Width > 2000 || image.Height > 2000)
                {
                    image.Resize(new MagickGeometry(2000, 2000)
                    {
                        IgnoreAspectRatio = false
                    });
                }

                using var output = new MemoryStream();
                image.Write(output);

                var convertedFileName = Path.ChangeExtension(photo.FileName, ".jpg") ?? $"photo-{index + 1}.jpg";
                _logger.LogInformation(
                    "Photo extraction converted DNG to JPEG. Index={Index}, FileName={FileName}, SourceLengthBytes={SourceLengthBytes}, ConvertedLengthBytes={ConvertedLengthBytes}, Width={Width}, Height={Height}, ElapsedMs={ElapsedMs}",
                    index,
                    photo.FileName,
                    photo.Length,
                    output.Length,
                    image.Width,
                    image.Height,
                    conversionStopwatch.ElapsedMilliseconds);

                return new NormalizedRecipePhoto(
                    photo.FileName,
                    convertedFileName,
                    "image/jpeg",
                    output.ToArray(),
                    true,
                    photo.ContentType,
                    photo.Length);
            }
            catch (Exception ex) when (ex is MagickException or NotSupportedException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Photo extraction failed to convert DNG. Index={Index}, FileName={FileName}, ContentType={ContentType}, LengthBytes={LengthBytes}, ElapsedMs={ElapsedMs}",
                    index,
                    photo.FileName,
                    photo.ContentType,
                    photo.Length,
                    conversionStopwatch.ElapsedMilliseconds);
                throw new ArgumentException($"\"{photo.FileName}\" could not be converted from DNG. Try retaking the photo without ProRAW or choose a JPEG version.");
            }
        }

        private static decimal ClampUnit(decimal value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private static RecipeCoverCrop? ToCoverCrop(CoverImageCandidate? candidate, int photoCount)
        {
            if (candidate?.SourceImageIndex is null ||
                candidate.X is null ||
                candidate.Y is null ||
                candidate.Width is null ||
                candidate.Height is null)
            {
                return null;
            }

            var sourceIndex = candidate.SourceImageIndex.Value - 1;
            if (sourceIndex < 0 || sourceIndex >= photoCount)
                return null;

            var x = ClampUnit(candidate.X.Value);
            var y = ClampUnit(candidate.Y.Value);
            var width = ClampUnit(candidate.Width.Value);
            var height = ClampUnit(candidate.Height.Value);

            if (width < 0.08m || height < 0.08m)
                return null;

            if (x + width > 1)
                width = 1 - x;
            if (y + height > 1)
                height = 1 - y;

            if (width < 0.08m || height < 0.08m)
                return null;

            return new RecipeCoverCrop(sourceIndex, x, y, width, height, candidate.Reason);
        }

        public NormalizedRecipePhoto? CreateCroppedCoverPhoto(IReadOnlyList<NormalizedRecipePhoto> photos, RecipeCoverCrop? crop)
        {
            if (crop is null || crop.SourceImageIndex < 0 || crop.SourceImageIndex >= photos.Count)
                return null;

            var source = photos[crop.SourceImageIndex];
            var cropStopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var image = new MagickImage(source.Bytes);
                var imageWidth = (int)Math.Min(image.Width, int.MaxValue);
                var imageHeight = (int)Math.Min(image.Height, int.MaxValue);
                var cropX = Math.Clamp((int)Math.Round(imageWidth * crop.X), 0, Math.Max(0, imageWidth - 1));
                var cropY = Math.Clamp((int)Math.Round(imageHeight * crop.Y), 0, Math.Max(0, imageHeight - 1));
                var cropWidth = Math.Clamp((int)Math.Round(imageWidth * crop.Width), 1, imageWidth - cropX);
                var cropHeight = Math.Clamp((int)Math.Round(imageHeight * crop.Height), 1, imageHeight - cropY);

                image.Crop(new MagickGeometry(cropX, cropY, (uint)cropWidth, (uint)cropHeight));
                image.AutoOrient();
                image.Strip();
                image.Format = MagickFormat.Jpeg;
                image.Quality = 88;

                if (image.Width > 1400 || image.Height > 1400)
                {
                    image.Resize(new MagickGeometry(1400, 1400)
                    {
                        IgnoreAspectRatio = false
                    });
                }

                using var output = new MemoryStream();
                image.Write(output);
                var fileName = Path.ChangeExtension(source.FileName, ".cover.jpg") ?? $"recipe-cover-{crop.SourceImageIndex + 1}.jpg";

                _logger.LogInformation(
                    "Photo extraction cropped suggested cover image. SourceImageIndex={SourceImageIndex}, FileName={FileName}, CropX={CropX}, CropY={CropY}, CropWidth={CropWidth}, CropHeight={CropHeight}, OutputBytes={OutputBytes}, Reason={Reason}, ElapsedMs={ElapsedMs}",
                    crop.SourceImageIndex,
                    source.FileName,
                    cropX,
                    cropY,
                    cropWidth,
                    cropHeight,
                    output.Length,
                    crop.Reason,
                    cropStopwatch.ElapsedMilliseconds);

                return new NormalizedRecipePhoto(
                    source.OriginalFileName,
                    fileName,
                    "image/jpeg",
                    output.ToArray(),
                    true,
                    source.PostedContentType,
                    source.SourceLength);
            }
            catch (Exception ex) when (ex is MagickException or NotSupportedException or InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Photo extraction failed to crop suggested cover image. SourceImageIndex={SourceImageIndex}, FileName={FileName}, Reason={Reason}, ElapsedMs={ElapsedMs}",
                    crop.SourceImageIndex,
                    source.FileName,
                    crop.Reason,
                    cropStopwatch.ElapsedMilliseconds);
                return null;
            }
        }

        public async Task<List<NormalizedRecipePhoto>> NormalizeRecipePhotosAsync(List<IFormFile> photos)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            if (photos == null || photos.Count == 0)
            {
                _logger.LogWarning("Photo extraction requested with no photos.");
                throw new ArgumentException("Please select at least one photo.");
            }
            if (photos.Count > 3)
            {
                _logger.LogWarning("Photo extraction rejected too many photos. PhotoCount={PhotoCount}", photos.Count);
                throw new ArgumentException("Please select up to 3 photos.");
            }

            _logger.LogInformation("Photo extraction validation started. PhotoCount={PhotoCount}", photos.Count);
            var normalizedPhotos = new List<NormalizedRecipePhoto>();
            for (var i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                var isDng = IsDngImage(photo);
                var maxBytes = isDng ? DngPhotoMaxBytes : StandardPhotoMaxBytes;
                if (photo.Length > maxBytes)
                {
                    _logger.LogWarning(
                        "Photo extraction rejected oversized file. Index={Index}, FileName={FileName}, LengthBytes={LengthBytes}, MaxBytes={MaxBytes}, IsDng={IsDng}",
                        i,
                        photo.FileName,
                        photo.Length,
                        maxBytes,
                        isDng);
                    var maxMb = maxBytes / 1024 / 1024;
                    throw new ArgumentException($"\"{photo.FileName}\" exceeds the {maxMb} MB limit. Please use a smaller image.");
                }

                var mimeType = GetAllowedImageMimeType(photo);
                if (mimeType is null && isDng)
                {
                    normalizedPhotos.Add(ConvertDngToJpeg(photo, i));
                    continue;
                }

                if (mimeType is null)
                {
                    _logger.LogWarning(
                        "Photo extraction rejected unsupported file signature. Index={Index}, FileName={FileName}, ContentType={ContentType}, LengthBytes={LengthBytes}",
                        i,
                        photo.FileName,
                        photo.ContentType,
                        photo.Length);
                    throw new ArgumentException($"\"{photo.FileName}\" does not appear to be a valid JPEG, PNG, or WebP image.");
                }

                using var ms = new MemoryStream();
                await photo.CopyToAsync(ms);
                normalizedPhotos.Add(new NormalizedRecipePhoto(
                    photo.FileName,
                    photo.FileName,
                    mimeType,
                    ms.ToArray(),
                    false,
                    photo.ContentType,
                    photo.Length));

                _logger.LogInformation(
                    "Photo extraction accepted file. Index={Index}, FileName={FileName}, PostedContentType={PostedContentType}, DetectedMimeType={DetectedMimeType}, LengthBytes={LengthBytes}",
                    i,
                    photo.FileName,
                    photo.ContentType,
                    mimeType,
                    photo.Length);
            }

            _logger.LogInformation(
                "Photo extraction normalization completed. PhotoCount={PhotoCount}, ConvertedCount={ConvertedCount}, TotalElapsedMs={TotalElapsedMs}",
                normalizedPhotos.Count,
                normalizedPhotos.Count(p => p.WasConverted),
                stopwatch.ElapsedMilliseconds);

            return normalizedPhotos;
        }

        public async Task<ImportRecipeVM> ExtractRecipeFromPhotosAsync(List<IFormFile> photos)
        {
            var normalizedPhotos = await NormalizeRecipePhotosAsync(photos);
            return await ExtractRecipeFromNormalizedPhotosAsync(normalizedPhotos);
        }

        public async Task<ImportRecipeVM> ExtractRecipeFromNormalizedPhotosAsync(IReadOnlyList<NormalizedRecipePhoto> photos)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var visionClient = _oaiClient.GetChatClient("gpt-4o");

            var contentParts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(PhotoExtractionPrompt)
            };
            foreach (var photo in photos)
            {
                _logger.LogInformation(
                    "Photo extraction loaded image bytes. FileName={FileName}, OriginalFileName={OriginalFileName}, DetectedMimeType={DetectedMimeType}, ByteCount={ByteCount}, WasConverted={WasConverted}",
                    photo.FileName,
                    photo.OriginalFileName,
                    photo.MimeType,
                    photo.Bytes.Length,
                    photo.WasConverted);
                contentParts.Add(ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(photo.Bytes),
                    photo.MimeType));
            }

            List<ChatMessage> messages =
            [
                ChatMessage.CreateSystemMessage("You are a strict JSON generator."),
                ChatMessage.CreateUserMessage(contentParts),
            ];

            _logger.LogInformation("Photo extraction OpenAI request started. PhotoCount={PhotoCount}, ConvertedCount={ConvertedCount}", photos.Count, photos.Count(p => p.WasConverted));
            var openAiStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var completion = await visionClient.CompleteChatAsync(messages,
                new ChatCompletionOptions { Temperature = 0 })
                .WaitAsync(TimeSpan.FromSeconds(90));
            _logger.LogInformation(
                "Photo extraction OpenAI request completed. ElapsedMs={ElapsedMs}, TotalElapsedMs={TotalElapsedMs}",
                openAiStopwatch.ElapsedMilliseconds,
                stopwatch.ElapsedMilliseconds);

            var text = StripMarkdownFences(completion.Value.Content[0].Text);

            var extracted = JsonSerializer.Deserialize<PhotoExtractionResult>(text)
                ?? throw new InvalidOperationException("Could not parse recipe data from the photo.");

            _logger.LogInformation(
                "Photo extraction parsed response. TotalElapsedMs={TotalElapsedMs}, TitleLength={TitleLength}, IngredientCount={IngredientCount}, StepCount={StepCount}",
                stopwatch.ElapsedMilliseconds,
                extracted.Title?.Length ?? 0,
                extracted.Ingredients?.Count ?? 0,
                extracted.Steps?.Count ?? 0);

            var ingredients = extracted.Ingredients ?? new();
            var steps = extracted.Steps ?? new();
            var coverCrop = ToCoverCrop(extracted.CoverImage, photos.Count);

            if (coverCrop is null)
            {
                _logger.LogInformation("Photo extraction did not return a usable cover crop.");
            }
            else
            {
                _logger.LogInformation(
                    "Photo extraction returned cover crop. SourceImageIndex={SourceImageIndex}, X={X}, Y={Y}, Width={Width}, Height={Height}, Reason={Reason}",
                    coverCrop.SourceImageIndex,
                    coverCrop.X,
                    coverCrop.Y,
                    coverCrop.Width,
                    coverCrop.Height,
                    coverCrop.Reason);
            }

            return new ImportRecipeVM
            {
                Title = extracted.Title ?? "Untitled",
                Ingredients = ingredients.Select(i => new ImportIngredientVM
                {
                    Name = i.Name,
                    CleanName = i.Name,
                    Amount = i.Quantity ?? 0,
                    Unit = i.Unit ?? string.Empty
                }).ToList(),
                Steps = steps,
                SuggestedCoverSourceImageIndex = coverCrop?.SourceImageIndex,
                SuggestedCoverX = coverCrop?.X,
                SuggestedCoverY = coverCrop?.Y,
                SuggestedCoverWidth = coverCrop?.Width,
                SuggestedCoverHeight = coverCrop?.Height,
                SuggestedCoverReason = coverCrop?.Reason
            };
        }

        public  class IngredientParseResponse
        {
            [JsonPropertyName("items")]
            public List<ParsedIngredientItem> Items { get; set; } = new();
        }

        public  class ParsedIngredientItem
        {
            // parsed quantity (decimal) or null
            [JsonPropertyName("quantity")]
            public decimal? Quantity { get; set; }

            // normalized unit (tsp, tbsp, cup, oz, etc) or null
            [JsonPropertyName("unit")]
            public string? Unit { get; set; }

            // normalized unit (tsp, tbsp, cup, oz, etc) or null
            [JsonPropertyName("originalQuantity")]
            public string? OriginalAmount { get; set; }

            // clean display name (UI)
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";

            // canonical name for DB matching
            [JsonPropertyName("canonicalName")]
            public string CanonicalName { get; set; } = "";
        }

    }
}
