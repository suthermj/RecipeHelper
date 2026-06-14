
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Services
{
    
    public class IngredientsService
    {
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
              ]
            }

            Rules:
            - quantity: decimal number or null (convert fractions: 1/2 → 0.5, 3/4 → 0.75, 1/3 → 0.33)
            - unit: use one of: tsp, tbsp, cup, oz, fl oz, lb, g, ml, l, pt, qt — or null for unitless items (e.g. 2 eggs)
            - name: clean display name with no quantity or unit prefix
            - steps: complete cooking instructions in order; each instruction is one string
            - If multiple images are provided, treat them as pages of the same recipe
            - Return only valid JSON — no markdown code fences, no extra text
            """;

        private sealed record PhotoExtractionResult(
            [property: JsonPropertyName("title")] string Title,
            [property: JsonPropertyName("ingredients")] List<RawExtractedIngredient> Ingredients,
            [property: JsonPropertyName("steps")] List<string> Steps);

        private sealed record RawExtractedIngredient(
            [property: JsonPropertyName("quantity")] decimal? Quantity,
            [property: JsonPropertyName("unit")] string? Unit,
            [property: JsonPropertyName("name")] string Name);

        public async Task<ImportRecipeVM> ExtractRecipeFromPhotosAsync(List<IFormFile> photos)
        {
            var allowed = new[] { "image/jpeg", "image/png", "image/webp" };

            if (photos == null || photos.Count == 0)
                throw new ArgumentException("Please select at least one photo.");
            if (photos.Count > 3)
                throw new ArgumentException("Please select up to 3 photos.");
            foreach (var photo in photos)
            {
                if (photo.Length > 15 * 1024 * 1024)
                    throw new ArgumentException($"\"{photo.FileName}\" exceeds the 15 MB limit. Please use a smaller image.");
                if (!allowed.Contains(photo.ContentType?.ToLower()))
                    throw new ArgumentException($"Unsupported file type \"{photo.ContentType}\". Please use JPEG, PNG, or WebP.");
            }

            var visionClient = _oaiClient.GetChatClient("gpt-4o");

            var contentParts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart(PhotoExtractionPrompt)
            };
            foreach (var photo in photos)
            {
                using var ms = new MemoryStream();
                await photo.CopyToAsync(ms);
                contentParts.Add(ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(ms.ToArray()),
                    photo.ContentType));
            }

            List<ChatMessage> messages =
            [
                ChatMessage.CreateSystemMessage("You are a strict JSON generator."),
                ChatMessage.CreateUserMessage(contentParts),
            ];

            var completion = await visionClient.CompleteChatAsync(messages,
                new ChatCompletionOptions { Temperature = 0 });

            var text = StripMarkdownFences(completion.Value.Content[0].Text);

            var extracted = JsonSerializer.Deserialize<PhotoExtractionResult>(text)
                ?? throw new InvalidOperationException("Could not parse recipe data from the photo.");

            return new ImportRecipeVM
            {
                Title = extracted.Title ?? "Untitled",
                Ingredients = extracted.Ingredients.Select(i => new ImportIngredientVM
                {
                    Name = i.Name,
                    CleanName = i.Name,
                    Amount = i.Quantity ?? 0,
                    Unit = i.Unit ?? string.Empty
                }).ToList(),
                Steps = extracted.Steps ?? new()
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
