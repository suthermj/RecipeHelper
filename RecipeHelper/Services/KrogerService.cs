using Newtonsoft.Json;
using System.Net.Http.Headers;
using RecipeHelper.Models.Kroger;
using System.Text;
using Azure.Core;

namespace RecipeHelper.Services
{
    public class KrogerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration; // Assuming you store your API keys and other settings in appsettings.json
        private readonly ILogger<KrogerService> _logger;
        private readonly string _baseUri;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _locationId;
        public KrogerService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KrogerService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Kroger:baseUri"];
            _locationId = _configuration["Kroger:mariemontLocationId"];
            _clientId = _configuration["Kroger:clientId"];
            _clientSecret = _configuration["Kroger:clientSecret"];
        }

        public async Task<string?> GetProductToken()
        {
            var client = _httpClientFactory.CreateClient();
            string encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("scope", "product.compact") // Adjust the scope according to your needs
            });
            var url = $"{_baseUri}/connect/oauth2/token?grant_type=client_credentials&scope=product.compact";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);
            var response = await client.PostAsync(url, requestBody);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TokenResponse>(content);
                return result?.Token;
            }
            else
            {
                _logger.LogError("Error retrieving Kroger access token.");
            }

            return null;
        }

        public async Task<List<Product>> SearchProductByFilter(string filterTerm)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetProductToken();

            if (token != null)
            {
                var url = $"{_baseUri}/products?filter.term={filterTerm}&filter.locationId=01400421";
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var krogerProducts = JsonConvert.DeserializeObject<KrogerProductSearchResponse>(content);

                    if (krogerProducts != null)
                    {
                        return TransformKrogerResponseToProducts(krogerProducts);
                    }
                    return null;
                }
                else
                {
                    _logger.LogError("Error searching for product");
                    return null;
                }
            }

            return null;
        }

        public List<Product> TransformKrogerResponseToProducts(KrogerProductSearchResponse response)
        {
            var products = new List<Product>();

            if (response?.data != null)
            {
                foreach (var datum in response.data)
                {
                    var product = new Product
                    {
                        ProductId = datum.productId,
                        Upc = datum.upc,
                        Categories = datum.categories.ToList(),
                        Description = datum.description,
                        SoldBy = datum.items.FirstOrDefault()?.soldBy ?? "N/A", // Assuming the first item is representative
                        Size = datum.items.FirstOrDefault()?.size ?? "N/A",
                        Price = datum.items.FirstOrDefault()?.price ?? new Price() // Handling potential nulls
                    };

                    products.Add(product);
                }
            }

            return products;
        }
    }
}
