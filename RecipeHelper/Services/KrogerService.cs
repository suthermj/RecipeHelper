using Newtonsoft.Json;
using System.Net.Http.Headers;
using RecipeHelper.Models.Kroger;
using System.Text;
using Azure.Core;
using Microsoft.AspNetCore.Mvc.Filters;

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
                    var searchResponse = JsonConvert.DeserializeObject<KrogerProductSearchResponse>(content);

                    if (searchResponse?.data != null)
                    {
                        return TransformKrogerResponseToProducts(searchResponse.data.ToList());
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

        public List<Product> TransformKrogerResponseToProducts(List<KrogerProduct> krogerProducts)
        {
            var products = new List<Product>();

            if (krogerProducts != null)
            {
                foreach (var krogerProduct in krogerProducts)
                {
                    var product = new Product
                    {
                        ProductId = krogerProduct.productId,
                        upc = krogerProduct.upc,
                        categories = krogerProduct.categories?.ToList(),
                        description = krogerProduct.description,
                        soldBy = krogerProduct.items.FirstOrDefault()?.soldBy ?? "N/A", // Assuming the first item is representative
                        size = krogerProduct.items?.FirstOrDefault()?.size ?? "N/A",
                        regularPrice = krogerProduct.items.FirstOrDefault()?.price?.regular ?? 0, // Handling potential nulls
                        promoPrice = krogerProduct.items.FirstOrDefault()?.price?.regular ?? 0, // Handling potential nulls
                        stockLevel = krogerProduct.items.FirstOrDefault()?.inventory?.stockLevel ?? "N/A"
                    };

                    product.onSale = product.promoPrice != product.regularPrice ? true : false;

                    products.Add(product);
                }
            }

            return products;
        }

        public async Task<Product> GetProductDetails(string productId)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetProductToken();

            if (token != null)
            {
                var url = $"{_baseUri}/products/{productId}?filter.locationId=01400421";
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var searchResponse = JsonConvert.DeserializeObject<KrogerProductDetailsResponse>(content);

                    if (searchResponse != null)
                    {
                        return TransformKrogerResponseToProducts(new List<KrogerProduct> { searchResponse.data })[0];
                    }
                    return null;
                }
                else
                {
                    _logger.LogError("Error getting product details");
                    return null;
                }
            }

            return null;
        }

        public async Task<bool> AddToCart()
        {



            return true;
        }

    }
}
