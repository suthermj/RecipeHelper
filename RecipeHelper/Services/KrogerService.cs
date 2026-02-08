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

            _logger.LogInformation("Converting {count} ingredients to Kroger cart items", vm.Items.Count);

            foreach (var item in vm.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Upc))
                {
                    _logger.LogWarning("Ingredient has no UPC, skipping.");
                    continue;
                }

                var krogerProduct = await GetProductDetails(item.Upc);
                if (krogerProduct == null)
                {
                    _logger.LogWarning("Could not fetch product details for UPC {upc}, skipping.", item.Upc);
                    continue;
                }

                var pack = KrogerPackInfo.BuildPackInfo(krogerProduct);
                var cartItem = krogerProduct.ToDetailedCartItem();
                cartItem.OriginalIngredient = $"{item.Quantity:0.##} {item.Measurement}";
                cartItem.KrogerPackSize = krogerProduct.size;
                string? conversionNote = null;

                _logger.LogInformation(
                    "Converting: {qty} {meas} → Kroger '{name}' (size={size}, soldBy={soldBy}, dim={dim})",
                    item.Quantity, item.Measurement, krogerProduct.name, krogerProduct.size,
                    pack.SoldByEffective, pack.Dimension);

                var ingredientUnit = UnitConverter.Parse(item.Measurement);
                var ingredientDim = UnitConverter.GetDimension(ingredientUnit);

                // If product size couldn't be parsed, fall back to raw quantity
                if (!pack.ParsedOk)
                {
                    cartItem.Quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity));
                    conversionNote = "Could not parse product size, using ingredient quantity as-is";
                    _logger.LogWarning("Could not parse size '{size}' for UPC {upc}", krogerProduct.size, item.Upc);
                }
                // BRANCH 1: Weight-sold items (produce/deli priced per-lb)
                // Kroger expects quantity in the unit they price by (usually lb)
                else if (pack.SoldByEffective.Equals("WEIGHT", StringComparison.OrdinalIgnoreCase))
                {
                    cartItem.Quantity = ConvertForWeightSoldItem(item, ingredientUnit, ingredientDim, krogerProduct.name, out conversionNote);
                }
                // BRANCH 2: Both ingredient and product are count-based
                else if (ingredientDim == MeasureDimension.Count &&
                         (pack.Dimension == PackDimension.Unit || pack.Dimension == PackDimension.Composite))
                {
                    var packCount = pack.CountEach ?? pack.PrimaryQty ?? 1;
                    cartItem.Quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity / packCount));
                }
                // BRANCH 3: Ingredient is count-based but product is weight/volume
                else if (ingredientDim == MeasureDimension.Count)
                {
                    cartItem.Quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity));
                    conversionNote = $"Ingredient is counted but product is sold by {pack.Dimension} — using raw count";
                }
                // BRANCH 4: Same dimension (both volume or both weight)
                else if (AreSameDimension(ingredientDim, pack.Dimension))
                {
                    cartItem.Quantity = ConvertSameDimension(item, ingredientUnit, pack, out conversionNote);
                }
                // BRANCH 5: Cross-dimension (volume ↔ weight)
                else if (IsCrossDimension(ingredientDim, pack.Dimension))
                {
                    cartItem.Quantity = ConvertCrossDimension(item, ingredientUnit, ingredientDim, pack, krogerProduct.name, out conversionNote);
                }
                // BRANCH 6: Fallback
                else
                {
                    cartItem.Quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity));
                    conversionNote = "Could not determine conversion method";
                }

                cartItem.ConversionNote = conversionNote;
                cartItems.Add(cartItem);

                _logger.LogInformation("Result: {qty}x '{name}' {note}",
                    cartItem.Quantity, cartItem.Name, conversionNote ?? "OK");
            }

            return cartItems;
        }

        private int ConvertForWeightSoldItem(CartItemVM item, MeasureUnit ingredientUnit,
            MeasureDimension ingredientDim, string productName, out string? conversionNote)
        {
            conversionNote = null;

            if (ingredientDim == MeasureDimension.Weight)
            {
                // Convert ingredient to pounds (Kroger weight items are per-lb)
                var inPounds = UnitConverter.Convert(item.Quantity, ingredientUnit, MeasureUnit.Pound);
                if (inPounds.HasValue)
                    return Math.Max(1, (int)Math.Ceiling(inPounds.Value));
            }
            else if (ingredientDim == MeasureDimension.Volume)
            {
                // Cross-dimension: try density table
                var density = DensityTable.GetDensity(productName);
                var teaspoons = UnitConverter.ToBase(item.Quantity, ingredientUnit);
                if (teaspoons.HasValue)
                {
                    var effectiveDensity = density ?? 1.0m;
                    var grams = DensityTable.VolumeToGrams(teaspoons.Value, effectiveDensity);
                    var pounds = grams / 453.592m;

                    if (density == null)
                        conversionNote = "Weight-sold item: used default density (water) for volume→weight";

                    return Math.Max(1, (int)Math.Ceiling(pounds));
                }
            }

            conversionNote = "Weight-sold item: could not convert, using raw quantity";
            return Math.Max(1, (int)Math.Ceiling(item.Quantity));
        }

        private static int ConvertSameDimension(CartItemVM item, MeasureUnit ingredientUnit,
            KrogerPackInfo pack, out string? conversionNote)
        {
            conversionNote = null;

            var krogerUnit = UnitConverter.Parse(pack.PrimaryUnit);
            var ingredientBase = UnitConverter.ToBase(item.Quantity, ingredientUnit);
            var krogerBase = UnitConverter.ToBase(pack.PrimaryQty ?? 0, krogerUnit);

            if (ingredientBase.HasValue && krogerBase.HasValue && krogerBase.Value > 0)
            {
                var ratio = ingredientBase.Value / krogerBase.Value;
                return Math.Max(1, (int)Math.Ceiling(ratio));
            }

            conversionNote = "Same dimension but could not compute ratio";
            return Math.Max(1, (int)Math.Ceiling(item.Quantity));
        }

        private int ConvertCrossDimension(CartItemVM item, MeasureUnit ingredientUnit,
            MeasureDimension ingredientDim, KrogerPackInfo pack, string productName,
            out string? conversionNote)
        {
            conversionNote = null;

            var krogerUnit = UnitConverter.Parse(pack.PrimaryUnit);
            var density = DensityTable.GetDensity(productName);
            var effectiveDensity = density ?? 1.0m;

            if (density == null)
                conversionNote = "Cross-dimension: used default density (water) — review quantity";

            decimal ingredientGrams;
            decimal krogerGrams;

            if (ingredientDim == MeasureDimension.Volume)
            {
                // Ingredient is volume, product is weight
                var tsp = UnitConverter.ToBase(item.Quantity, ingredientUnit);
                ingredientGrams = DensityTable.VolumeToGrams(tsp ?? 0, effectiveDensity);
                krogerGrams = UnitConverter.ToBase(pack.PrimaryQty ?? 0, krogerUnit) ?? 0;
            }
            else
            {
                // Ingredient is weight, product is volume
                ingredientGrams = UnitConverter.ToBase(item.Quantity, ingredientUnit) ?? 0;
                var krogerTsp = UnitConverter.ToBase(pack.PrimaryQty ?? 0, krogerUnit);
                krogerGrams = DensityTable.VolumeToGrams(krogerTsp ?? 0, effectiveDensity);
            }

            if (krogerGrams > 0)
            {
                var ratio = ingredientGrams / krogerGrams;
                return Math.Max(1, (int)Math.Ceiling(ratio));
            }

            conversionNote = "Cross-dimension: could not compute ratio";
            return Math.Max(1, (int)Math.Ceiling(item.Quantity));
        }

        private static bool AreSameDimension(MeasureDimension ingredientDim, PackDimension packDim)
        {
            return (ingredientDim == MeasureDimension.Volume &&
                    (packDim == PackDimension.Volume || packDim == PackDimension.Composite)) ||
                   (ingredientDim == MeasureDimension.Weight &&
                    (packDim == PackDimension.Weight || packDim == PackDimension.Composite));
        }

        private static bool IsCrossDimension(MeasureDimension ingredientDim, PackDimension packDim)
        {
            return (ingredientDim == MeasureDimension.Volume &&
                    (packDim == PackDimension.Weight)) ||
                   (ingredientDim == MeasureDimension.Weight &&
                    (packDim == PackDimension.Volume));
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
