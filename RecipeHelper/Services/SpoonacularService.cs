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
            _logger.LogInformation("Spoonacular ImportRecipe started. RecipeUrl={RecipeUrl}", recipeUrl);
            try
            {
                var response = await client.GetAsync(requestUri);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var recipe = JsonConvert.DeserializeObject<Recipe>(content);
                    _logger.LogInformation("Spoonacular ImportRecipe succeeded. RecipeUrl={RecipeUrl}", recipeUrl);
                    return recipe;
                }
                else
                {
                    _logger.LogWarning("Spoonacular ImportRecipe failed. RecipeUrl={RecipeUrl}, StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}",
                        recipeUrl, (int)response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Exception type/message are put directly in the message template (not
                // just passed as the LogError exception argument) -- see the matching
                // comment in StorageService.StoreRecipeImage for why.
                _logger.LogError(ex, "Spoonacular ImportRecipe threw. RecipeUrl={RecipeUrl}, ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    recipeUrl, ex.GetType().FullName, ex.Message);
                return null;
            }
        }

        public async Task<ImportRecipeVM> Import(string recipeUrl)
        {
            _logger.LogInformation("Spoonacular Import (JSON-LD fallback) started. RecipeUrl={RecipeUrl}", recipeUrl);
            var http = new HttpClient();
            var recipeNode = await RecipeJsonLdExtractor.ExtractRecipeNodeAsync(http, recipeUrl);

            if (recipeNode is null)
            {
                _logger.LogWarning("Spoonacular Import (JSON-LD fallback) found no recipe node. RecipeUrl={RecipeUrl}", recipeUrl);
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
                    Steps = recipe.RecipeInstructions.Select(x => System.Net.WebUtility.HtmlDecode(x.Text)).ToList(),
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

                _logger.LogInformation("Spoonacular Import (JSON-LD fallback) completed. RecipeUrl={RecipeUrl}, IngredientCount={IngredientCount}, StepCount={StepCount}",
                    recipeUrl, import.Ingredients.Count, import.Steps.Count);
                return import;
            }
        }
    }
}
