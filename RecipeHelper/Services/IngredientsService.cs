
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using OpenAI.Chat;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;

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

            var text = completion.Value.Content[0].Text;

            var result = JsonSerializer.Deserialize<CanonicalizeResult>(text)
                         ?? throw new InvalidOperationException("Invalid JSON from OpenAI");
            result.CanonicalName = Normalize(result.CanonicalName);

            return result;
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

            var text = completion.Value.Content[0].Text;

            var result = JsonSerializer.Deserialize<IngredientParseResponse>(text)
                         ?? throw new InvalidOperationException("Invalid JSON from OpenAI");
         
            return result;
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
