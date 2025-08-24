using System.Net.Http;
using Newtonsoft.Json;
using RecipeHelper.Models.Spoonacular;

namespace RecipeHelper.Services
{
    public class SpoonacularService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration; // Assuming you store your API keys and other settings in appsettings.json
        private readonly ILogger<SpoonacularService> _logger;
        private readonly string _baseUri;
        private readonly string _apiKey;

        public SpoonacularService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SpoonacularService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Spoonacular:baseUri"];
            _apiKey = _configuration["Spoonacular:apiKey"];
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
    }
}
