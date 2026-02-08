using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NuGet.Common;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.Kroger.Carts;
using RecipeHelper.Utility;

namespace RecipeHelper.Services
{
    public class KrogerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration; // Assuming you store your API keys and other settings in appsettings.json
        private readonly ILogger<KrogerService> _logger;
        private KrogerAuthService _krogerAuthService;
        private readonly string _baseUri;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly IMemoryCache _cache;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1); // static => shared across scopes
        private const string TokenCacheKey = "kroger:client-credentials-token";
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);


        public KrogerService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KrogerService> logger, KrogerAuthService krogerAuthService, IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Kroger:baseUri"];
            _clientId = _configuration["Kroger:clientId"];
            _clientSecret = _configuration["Kroger:clientSecret"];
            _krogerAuthService = krogerAuthService;
            _cache = memoryCache;
        }

        public async Task<string?> GetKrogerClientCredentialsToken()
        {
            string token = "";

            // check cache first
            if (_cache != null && _cache.TryGetValue<string>(TokenCacheKey, out token) && !string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
            
            // prevent all tasks from hammering token endpoint
            await _tokenLock.WaitAsync();

            try
            {
                // re-check cache inside lock
                if (_cache != null && _cache.TryGetValue<string>(TokenCacheKey, out token) && !string.IsNullOrWhiteSpace(token))
                {
                    return token;
                }

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

                    var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, result.ExpiresIn));

                    // Cache entry with absolute expiration slightly BEFORE true expiry (skew)
                    var ttl = TimeSpan.FromSeconds(Math.Max(5, result.ExpiresIn)) - RefreshSkew;
                    if (ttl < TimeSpan.FromSeconds(5)) ttl = TimeSpan.FromSeconds(5);

                    _cache.Set(TokenCacheKey, result.Token, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });

                    return result.Token;
                }
                else
                {
                    _logger.LogError("Error retrieving Kroger access token. [{status}]", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error retrieving Kroger access token: {message}", ex.Message);
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task<List<KrogerProductDto>?> SearchProductByFilter(string filterTerm)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();

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
                        return searchResponse.data.ToKrogerProducts();
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

        public async Task<KrogerProductDto?> GetProductDetails(string productId)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();
            var url = $"{_baseUri}/products/{productId}?filter.locationId=01400421";

            if (token != null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var searchResponse = JsonConvert.DeserializeObject<KrogerProductDetailsResponse>(content);

                    if (searchResponse != null)
                    {
                        return searchResponse.data.ToKrogerProduct();
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

        public async Task<bool> AddToCartAsync(AddToCartRequest addToCartRequest, string accessToken)
        {
            var auth = await _krogerAuthService.EnsureAccessTokenAsync();

            if (!auth.IsAuthorized || string.IsNullOrEmpty(auth.AccessToken))
            {
                _logger.LogError("User not authorized for Kroger APIs. Prompting re-login.");
                return false;
            }

            if (addToCartRequest == null || addToCartRequest.Items.Count == 0)
            {
                _logger.LogError("AddToCartVM is null or has no items.");
                return false;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var url = $"{_baseUri}/cart/add";

            try
            {
                _logger.LogInformation("Adding {itemCount} items to Kroger cart.", addToCartRequest.Items.Count);
                var jsonContent = JsonConvert.SerializeObject(addToCartRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await client.PutAsync(url, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error adding item to cart.{statusCode}, {reason}", response.StatusCode, response.ReasonPhrase);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred while adding items to cart: {ex.Message}");
                return false;
            }

            return true;
        }

        public async Task<List<DetailedCartItem>> ConvertIngredientsToCartItems(AddToCartVM vm)
        {
            List<DetailedCartItem> cartItems = new();

            _logger.LogInformation("Converting [{count} ingredients to Kroger Cart items", vm.Items.Count);

            foreach (var item in vm.Items)
            {
                var krogerProduct = await GetProductDetails(item.Upc);

                var krogerPack = KrogerPackInfo.BuildPackInfo(krogerProduct);

                _logger.LogInformation("Kroger product info Name: {name} SoldBy: {soldBy} SizeUnit: {sizeUnit} UnitOfMeasure: {uof}", krogerProduct.name, krogerProduct.soldBy, krogerProduct.sizeUnit, krogerProduct.unitOfMeasure);

                if (krogerPack.Dimension == PackDimension.Unit)
                {
                    DetailedCartItem cartItem = null;
                    // ingredient measured in units (lasagna noodles / onions / ect)
                    if (item.Measurement.Equals("UNIT", StringComparison.OrdinalIgnoreCase))
                    {
                        cartItem = krogerProduct.ToDetailedCartItem();
                        cartItem.Quantity = (int)Math.Ceiling(item.Quantity);
                        cartItems.Add(cartItem);
                    }
                    else
                    {
                        try
                        {
                            var krogerProductMeasurementUnitType = MeasurementHelper.GetMeasurementUnitType(krogerProduct.sizeUnit);

                            var krogerProductNormalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(krogerProduct.sizeUnit);
                            var ingredientNormalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(item.Measurement);
                            decimal krogerProductAmountSmallestUnit = 0;
                            decimal ingredientAmountSmallestUnit = 0;

                            if (krogerProductMeasurementUnitType == "Volume")
                            {
                                krogerProductAmountSmallestUnit = MeasurementHelper.ConvertVolumeToBaseUnit(krogerProductNormalizedMeasurementUnit, krogerProduct.sizeQuantity);
                                ingredientAmountSmallestUnit = MeasurementHelper.ConvertVolumeToBaseUnit(ingredientNormalizedMeasurementUnit, item.Quantity);

                            }
                            else
                            {
                                krogerProductAmountSmallestUnit = MeasurementHelper.ConvertWeightToBaseUnit(krogerProductNormalizedMeasurementUnit, krogerProduct.sizeQuantity);
                                ingredientAmountSmallestUnit = MeasurementHelper.ConvertWeightToBaseUnit(ingredientNormalizedMeasurementUnit, item.Quantity);
                            }

                            if (krogerProductAmountSmallestUnit == null || ingredientAmountSmallestUnit == null)
                            {
                                _logger.LogInformation("Could not convert measurement for product " + item.Upc + ", skipping.");
                                cartItem = krogerProduct.ToDetailedCartItem();
                                cartItem.Quantity = 0;
                                cartItems.Add(cartItem);
                                continue;
                            }

                            var quantityNeeded = (int)Math.Ceiling(ingredientAmountSmallestUnit / krogerProductAmountSmallestUnit);

                            cartItem = krogerProduct.ToDetailedCartItem();
                            cartItem.Quantity = quantityNeeded;
                            cartItems.Add(cartItem);
                        }
                        catch
                        {
                            _logger.LogInformation("Could not parse measurement " + item.Measurement + " for product " + item.Upc + ", skipping.");
                            continue;

                        }
                    }
                }
                // usually produce or deli items
                else if (krogerProduct.soldBy.Equals("WEIGHT", StringComparison.OrdinalIgnoreCase))
                {

                    DetailedCartItem cartItem = krogerProduct.ToDetailedCartItem();
                    cartItem.Quantity = (int)Math.Ceiling(item.Quantity);
                    cartItems.Add(cartItem);
                }
                else
                {
                    _logger.LogInformation("Product " + krogerProduct.upc + " is not sold by unit, skipping for now.");
                }

            }

            return cartItems;
        }

        public async Task<List<DetailedCartItem>?> GetKrogerCartItemsAsync(string accessToken)
        {
            List<DetailedCartItem> products = new List<DetailedCartItem>();
            string url = $"{_baseUri}/carts";
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); // Replace with actual customer access token

            try
            {
                var cartsResponse = await client.GetAsync(url);

                if (!cartsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Error getting cart items. {statusCode} {reason}", cartsResponse.StatusCode, cartsResponse.ReasonPhrase);
                    return null;
                }

                var content = await cartsResponse.Content.ReadAsStringAsync();
                var cartItems = JsonConvert.DeserializeObject<KrogerGetCartsResponse>(content);

                foreach (var item in cartItems.data[0].items)
                {
                    var productDetails = await GetProductDetails(item.upc);
                    products.Add(productDetails.ToDetailedCartItem(item.quantity));
                }

                return products;

            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting cart items {ex}", ex.Message);
                return null;
            }
        }

    }
}
