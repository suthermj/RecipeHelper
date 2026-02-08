using System.Net.Http;
using System.Text.Json;
using Newtonsoft.Json;
using RecipeHelper.Models.Import;
using RecipeHelper.Models.Spoonacular;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Services
{
    public class SpoonacularService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration; // Assuming you store your API keys and other settings in appsettings.json
        private readonly ILogger<SpoonacularService> _logger;
        private readonly IngredientsService _ingredientService;
        private readonly string _baseUri;
        private readonly string _apiKey;

        public SpoonacularService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SpoonacularService> logger, IngredientsService ingredientsService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Spoonacular:baseUri"];
            _apiKey = _configuration["Spoonacular:apiKey"];
            _ingredientService = ingredientsService;
        }

        public async Task<Recipe> ImportRecipe(string recipeUrl)
        {
            var client = _httpClientFactory.CreateClient();
            var requestUri = $"{_baseUri}/recipes/extract?url={Uri.EscapeDataString(recipeUrl)}&apiKey={_apiKey}";
            try
            {
                var response = await client.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Recipe>(content);
                }
                else
                {
                    _logger.LogError($"Error retrieving recipe from Spoonacular: {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred while importing recipe: {ex.Message}");
                return null;
            }
        }

        public async Task<ImportRecipeVM> Import(string recipeUrl)
        {
            var http = new HttpClient();
            var recipeNode = await RecipeJsonLdExtractor.ExtractRecipeNodeAsync(http, recipeUrl);

            if (recipeNode is null)
            {
                return null;
            }
            else
            {
                var recipe = System.Text.Json.JsonSerializer.Deserialize<JsonLdRecipe>(recipeNode.Value.GetRawText());
                
                var ings = await _ingredientService.TransformRawIngredients(recipe.RecipeIngredient, CancellationToken.None);
                
                var import = new ImportRecipeVM
                {
                    Title = recipe.Name ?? "Imported Recipe",
                    Image = recipe.ExtractFirstImage(),
                    SummaryText = recipe.Description,
                    SourceUrl = recipeUrl,
                    Steps = recipe.RecipeInstructions.Select(x => x.Text).ToList(),
                    //ReadyInMinutes = recipe.TotalTime,
                    //Servings = recipe.RecipeYield,
                    Ingredients = ings.Items?
                        .Select(line => new ImportIngredientVM
                        {
                            CleanName = line.CanonicalName,
                            Amount = (decimal)line.Quantity,
                            DisplayAmount = line.OriginalAmount,
                            Unit = line.Unit,
                            Name = line.Name
                        })
                        .ToList() ?? new()
                };

                return import;
            }
        }
    }
}
