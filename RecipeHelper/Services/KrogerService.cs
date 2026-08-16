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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1); // static => shared across scopes
        private const string TokenCacheKey = "kroger:client-credentials-token";
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(60);

        // Single user-facing conversion-warning message. The mechanism-specific detail
        // (which dimension mismatched, which fallback fired, whether a density was
        // assumed) is genuinely useful for debugging and stays in the server logs via
        // the LogWarning/LogInformation calls at each site below -- but a shopper
        // reviewing the cart preview doesn't need any of that to decide whether to
        // double-check a quantity, so every conversionNote uses this same short text.
        private const string QuantityNeedsReviewNote = "Estimated — please verify quantity";


        public KrogerService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KrogerService> logger, KrogerAuthService krogerAuthService, IMemoryCache memoryCache, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Kroger:baseUri"];
            _clientId = _configuration["Kroger:clientId"];
            _clientSecret = _configuration["Kroger:clientSecret"];
            _krogerAuthService = krogerAuthService;
            _cache = memoryCache;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetLocationId()
        {
            var locationId = _httpContextAccessor.HttpContext?.Request.Cookies["KrogerLocationId"];
            return string.IsNullOrWhiteSpace(locationId)
                ? _configuration["Kroger:mariemontLocationId"] ?? "01400421"
                : locationId;
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
                // Exception type/message are put directly in the message template (not
                // just passed as the LogError exception argument) -- see the matching
                // comment in StorageService.StoreRecipeImage for why.
                _logger.LogError(ex, "Error retrieving Kroger access token. ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    ex.GetType().FullName, ex.Message);
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task<List<KrogerLocationDto>?> SearchLocations(string zipCode, int limit = 10)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();

            if (token == null) return null;

            var url = $"{_baseUri}/locations?filter.zipCode.near={zipCode}&filter.limit={limit}&filter.chain=Kroger";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var locationsResponse = JsonConvert.DeserializeObject<KrogerLocationsResponse>(content);

                return locationsResponse?.Data.Select(l => new KrogerLocationDto
                {
                    LocationId = l.LocationId,
                    Name = l.Name,
                    Address = $"{l.Address.AddressLine1}, {l.Address.City}, {l.Address.State} {l.Address.ZipCode}",
                    Phone = l.Phone,
                    Chain = l.Chain,
                    Latitude = l.Geolocation?.Latitude,
                    Longitude = l.Geolocation?.Longitude
                }).ToList();
            }

            _logger.LogError("Error searching for Kroger locations near {ZipCode}", zipCode);
            return null;
        }

        public async Task<List<KrogerLocationDto>?> SearchLocationsByLatLong(double latitude, double longitude, int limit = 10)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();

            if (token == null) return null;

            var url = $"{_baseUri}/locations?filter.latLong.near={latitude},{longitude}&filter.limit={limit}&filter.chain=Kroger";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var locationsResponse = JsonConvert.DeserializeObject<KrogerLocationsResponse>(content);

                return locationsResponse?.Data.Select(l => new KrogerLocationDto
                {
                    LocationId = l.LocationId,
                    Name = l.Name,
                    Address = $"{l.Address.AddressLine1}, {l.Address.City}, {l.Address.State} {l.Address.ZipCode}",
                    Phone = l.Phone,
                    Chain = l.Chain,
                    Latitude = l.Geolocation?.Latitude,
                    Longitude = l.Geolocation?.Longitude
                }).ToList();
            }

            _logger.LogError("Error searching for Kroger locations near {Latitude},{Longitude}", latitude, longitude);
            return null;
        }

        public async Task<List<KrogerProductDto>?> SearchProductByFilter(string filterTerm)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();

            if (token != null)
            {
                var url = $"{_baseUri}/products?filter.term={filterTerm}&filter.locationId={GetLocationId()}";
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

        /// <summary>
        /// Fetches product data (including aisle locations) for a batch of UPCs using the search endpoint,
        /// which returns aisle data far more reliably than the product details endpoint.
        /// Returns a map of UPC → KrogerProductDto.
        /// </summary>
        public async Task<Dictionary<string, KrogerProductDto>> GetProductsByUpcBatch(IEnumerable<string> upcs, string locationId)
        {
            var result = new Dictionary<string, KrogerProductDto>(StringComparer.OrdinalIgnoreCase);
            var upcList = upcs.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            if (!upcList.Any()) return result;

            var token = await GetKrogerClientCredentialsToken();
            if (token == null) return result;

            // Use the details endpoint per-UPC with locationId for store-specific aisle data.
            // filter.productId on the search endpoint silently ignores filter.locationId,
            // so the details endpoint is the only way to get store-specific aisle locations.
            var throttle = new SemaphoreSlim(5, 5);
            var tasks = upcList.Select(async upc =>
            {
                await throttle.WaitAsync();
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var url = $"{_baseUri}/products/{upc}?filter.locationId={locationId}";
                    var response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) return (upc, dto: (KrogerProductDto?)null);

                    var content = await response.Content.ReadAsStringAsync();
                    var detailsResponse = JsonConvert.DeserializeObject<KrogerProductDetailsResponse>(content);
                    var dto = detailsResponse?.data != null ? (KrogerProductDto?)detailsResponse.data.ToKrogerProduct() : null;
                    return (upc, dto);
                }
                finally { throttle.Release(); }
            });

            foreach (var (upc, dto) in await Task.WhenAll(tasks))
            {
                if (dto != null) result[upc] = dto;
            }

            return result;
        }

        public async Task<KrogerProductDto?> GetProductDetails(string productId)
        {
            var client = _httpClientFactory.CreateClient();
            var token = await GetKrogerClientCredentialsToken();
            var url = $"{_baseUri}/products/{productId}?filter.locationId={GetLocationId()}";

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
                    _logger.LogError("Error adding item to cart. StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
                    return false;
                }
                _logger.LogInformation("Successfully added items to Kroger cart.");
            }
            catch (Exception ex)
            {
                // Exception type/message are put directly in the message template (not
                // just passed as the LogError exception argument) -- see the matching
                // comment in StorageService.StoreRecipeImage for why.
                _logger.LogError(ex, "Exception occurred while adding items to Kroger cart. ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    ex.GetType().FullName, ex.Message);
                return false;
            }

            return true;
        }

        public async Task<ConvertIngredientsResult> ConvertIngredientsToCartItems(AddToCartVM vm)
        {
            List<SkippedCartItem> skipped = new();

            _logger.LogInformation("Converting {count} ingredients to Kroger cart items", vm.Items.Count);

            // Fetch all product details concurrently up front (throttled inside GetProductsByUpcBatch)
            // instead of one awaited HTTP round-trip per ingredient in the loop below -- with a
            // few dozen ingredients, sequential per-item lookups added up to ~20s of pure serialized
            // Kroger API latency on a single cart preview.
            var upcs = vm.Items.Where(i => !string.IsNullOrWhiteSpace(i.Upc)).Select(i => i.Upc);
            var productsByUpc = await GetProductsByUpcBatch(upcs, GetLocationId());

            // Resolve each ingredient to its Kroger product first, separating out anything
            // that can't be converted at all before any quantity math runs.
            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>();
            foreach (var item in vm.Items)
            {
                var itemName = string.IsNullOrWhiteSpace(item.Name) ? "Unknown ingredient" : item.Name;

                if (string.IsNullOrWhiteSpace(item.Upc))
                {
                    _logger.LogWarning("Ingredient {name} has no UPC, skipping.", itemName);
                    skipped.Add(new SkippedCartItem { Name = itemName, Reason = "No product mapped", Quantity = item.Quantity });
                    continue;
                }

                if (!productsByUpc.TryGetValue(item.Upc, out var krogerProduct))
                {
                    _logger.LogWarning("Could not fetch product details for UPC {upc} ({name}), skipping.", item.Upc, itemName);
                    skipped.Add(new SkippedCartItem { Name = itemName, Reason = "Product lookup failed", Quantity = item.Quantity });
                    continue;
                }

                resolved.Add((item, krogerProduct));
            }

            var cartItems = ConvertResolvedItemsToCartItems(resolved);
            return new ConvertIngredientsResult { Items = cartItems, Skipped = skipped };
        }

        // Pure computation half of ConvertIngredientsToCartItems, split out so it's
        // testable without a live Kroger product lookup -- callers (and
        // RecipeHelper.Tests, via InternalsVisibleTo) supply already-resolved
        // (ingredient, Kroger product) pairs.
        //
        // Groups by UPC BEFORE converting to a pack quantity, not after. Different
        // ingredient names across recipes (e.g. "garlic" vs "minced garlic") can
        // independently map to the same Kroger product -- if each is converted to a pack
        // quantity on its own and THEN summed, several small amounts that each round up
        // to "1 jar" individually add up to far more jars than the combined amount
        // actually needs (four small garlic amounts each rounding up to 1 jar == 4 jars
        // ordered for what was really ~1/3 of one jar). Summing the underlying need
        // first and rounding up once per UPC avoids that.
        internal List<DetailedCartItem> ConvertResolvedItemsToCartItems(List<(CartItemVM Item, KrogerProductDto Product)> resolved)
        {
            var cartItems = new List<DetailedCartItem>();

            foreach (var group in resolved.GroupBy(r => r.Item.Upc, StringComparer.OrdinalIgnoreCase))
            {
                var groupList = group.ToList();
                var krogerProduct = groupList[0].Product;
                var upc = groupList[0].Item.Upc;
                var itemName = string.IsNullOrWhiteSpace(groupList[0].Item.Name) ? "Unknown ingredient" : groupList[0].Item.Name;
                var pack = KrogerPackInfo.BuildPackInfo(krogerProduct);

                // Bucket by dimension (same shape as DinnerController.SubmitDinnerSelections'
                // own mixed-measurement aggregation) so amounts in compatible units (e.g.
                // teaspoons and tablespoons) combine into one total instead of staying as
                // separate rows just because their source ingredients used different units.
                decimal totalVolumeBase = 0, totalWeightBase = 0, totalCount = 0;
                bool hasVolume = false, hasWeight = false, hasCount = false;
                var originalParts = new List<string>();

                foreach (var (item, _) in groupList)
                {
                    originalParts.Add($"{item.Quantity:0.##} {item.Measurement}");
                    var unit = UnitConverter.Parse(item.Measurement);
                    switch (UnitConverter.GetDimension(unit))
                    {
                        case MeasureDimension.Volume:
                            totalVolumeBase += UnitConverter.ToBase(item.Quantity, unit) ?? 0;
                            hasVolume = true;
                            break;
                        case MeasureDimension.Weight:
                            totalWeightBase += UnitConverter.ToBase(item.Quantity, unit) ?? 0;
                            hasWeight = true;
                            break;
                        default:
                            totalCount += item.Quantity;
                            hasCount = true;
                            break;
                    }
                }

                var originalIngredient = string.Join(" + ", originalParts.Where(s => !string.IsNullOrWhiteSpace(s)));

                _logger.LogInformation(
                    "Converting UPC {upc} ({name}): volumeBase={vol}tsp weightBase={wt}g count={ct} → Kroger '{krogerName}' (size={size}, soldBy={soldBy}, dim={dim})",
                    upc, itemName, totalVolumeBase, totalWeightBase, totalCount,
                    krogerProduct.name, krogerProduct.size, pack.SoldByEffective, pack.Dimension);

                // At most one row per dimension bucket per UPC -- rare in practice (most
                // ingredients only ever need one dimension across all their sources), but
                // mirrors the same split DinnerController already does when a single
                // ingredient's uses can't be summed into one unit.
                void AddRow(decimal baseQuantity, string bucketMeasurement)
                {
                    // A synthetic CartItemVM already expressed in the dimension's base unit
                    // (teaspoons/grams/count) lets this reuse the exact same per-branch
                    // conversion logic as a single ingredient would use -- ToBase on the
                    // base unit itself is an identity conversion, so no double-conversion.
                    var bucketItem = new CartItemVM { Upc = upc, Name = itemName, Quantity = baseQuantity, Measurement = bucketMeasurement, Include = true };
                    var (quantity, note) = ComputeCartQuantity(bucketItem, krogerProduct, pack);

                    var cartItem = krogerProduct.ToDetailedCartItem();
                    cartItem.Quantity = quantity;
                    cartItem.OriginalIngredient = originalIngredient;
                    cartItem.KrogerPackSize = krogerProduct.size;
                    cartItem.ConversionNote = note;
                    cartItems.Add(cartItem);

                    _logger.LogInformation("Result: {qty}x '{name}' {note}", quantity, cartItem.Name, note ?? "OK");
                }

                if (hasVolume) AddRow(totalVolumeBase, "Teaspoons");
                if (hasWeight) AddRow(totalWeightBase, "Grams");
                if (hasCount) AddRow(totalCount, "Unit");
            }

            return cartItems;
        }

        // Extracted from the old per-item loop in ConvertIngredientsToCartItems so both a
        // single ingredient's amount and a per-UPC combined bucket amount (see AddRow
        // above) can run through the exact same branch selection.
        private (int Quantity, string? Note) ComputeCartQuantity(CartItemVM item, KrogerProductDto krogerProduct, KrogerPackInfo pack)
        {
            var ingredientUnit = UnitConverter.Parse(item.Measurement);
            var ingredientDim = UnitConverter.GetDimension(ingredientUnit);
            string? conversionNote = null;
            int quantity;

            // If product size couldn't be parsed, fall back to raw quantity -- but only
            // when that quantity is actually a count (e.g. "3" cloves), where treating it
            // as roughly the pack count is a reasonable guess. For Volume/Weight, `item`
            // here may be a per-UPC bucket already expressed in base units (teaspoons/
            // grams, see AddRow above) rather than the ingredient's original display
            // unit -- ceiling that raw number would wildly overstate the pack count (e.g.
            // "1.5 Cups" becomes 72 base teaspoons, producing a bogus Qty 72 instead of a
            // small number). There's no way to size a pack without a parseable size, so
            // default to 1 and flag it for a human to check instead of guessing a number
            // that looks precise but isn't.
            if (!pack.ParsedOk)
            {
                quantity = ingredientDim == MeasureDimension.Count
                    ? Math.Max(1, (int)Math.Ceiling(item.Quantity))
                    : 1;
                conversionNote = QuantityNeedsReviewNote;
                _logger.LogWarning("Could not parse size '{size}' for UPC {upc} -- quantity defaulted to 1", krogerProduct.size, item.Upc);
            }
            // BRANCH 1: Weight-sold items (produce/deli priced per-lb)
            // Kroger expects quantity in the unit they price by (usually lb)
            else if (pack.SoldByEffective.Equals("WEIGHT", StringComparison.OrdinalIgnoreCase))
            {
                quantity = ConvertForWeightSoldItem(item, ingredientUnit, ingredientDim, krogerProduct.name, out conversionNote);
            }
            // BRANCH 2: Both ingredient and product are count-based. pack.IsComposite
            // (not pack.Dimension == Composite -- that value no longer exists here,
            // see KrogerSizeParser) covers packs like "8 ct / 22 oz": the count
            // ingredient compares against the pack's CountEach regardless of what
            // dimension its primary (weight/volume) measurement is in.
            else if (ingredientDim == MeasureDimension.Count &&
                     (pack.Dimension == PackDimension.Unit || pack.IsComposite))
            {
                var packCount = pack.CountEach ?? pack.PrimaryQty ?? 1;
                quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity / packCount));
            }
            // BRANCH 3: Ingredient is count-based but product is weight/volume
            else if (ingredientDim == MeasureDimension.Count)
            {
                quantity = Math.Max(1, (int)Math.Ceiling(item.Quantity));
                conversionNote = QuantityNeedsReviewNote;
                _logger.LogInformation("Ingredient is counted but product for UPC {upc} is sold by {dim} -- using raw count", item.Upc, pack.Dimension);
            }
            // BRANCH 4: Same dimension (both volume or both weight)
            else if (AreSameDimension(ingredientDim, pack.Dimension))
            {
                quantity = ConvertSameDimension(item, ingredientUnit, pack, out conversionNote);
            }
            // BRANCH 5: Cross-dimension (volume ↔ weight)
            else if (IsCrossDimension(ingredientDim, pack.Dimension))
            {
                quantity = ConvertCrossDimension(item, ingredientUnit, ingredientDim, pack, krogerProduct.name, out conversionNote);
            }
            // BRANCH 6: Fallback -- only reachable for Volume/Weight ingredients (Count
            // is always caught by BRANCH 2/3 above), where `item.Quantity` is a base-unit
            // amount, not a pack-count-like number (see the !pack.ParsedOk comment above
            // for why ceiling-ing it directly would be wildly wrong). Default to 1.
            else
            {
                quantity = 1;
                conversionNote = QuantityNeedsReviewNote;
                _logger.LogWarning("Could not determine a conversion method for UPC {upc} -- quantity defaulted to 1", item.Upc);
            }

            return (quantity, conversionNote);
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
                    {
                        conversionNote = QuantityNeedsReviewNote;
                        _logger.LogInformation("Weight-sold item for UPC {upc}: no density found for '{name}', used default (water) for volume->weight", item.Upc, productName);
                    }

                    return Math.Max(1, (int)Math.Ceiling(pounds));
                }
            }

            conversionNote = QuantityNeedsReviewNote;
            _logger.LogWarning("Weight-sold item for UPC {upc}: could not convert, using raw quantity", item.Upc);
            return Math.Max(1, (int)Math.Ceiling(item.Quantity));
        }

        private int ConvertSameDimension(CartItemVM item, MeasureUnit ingredientUnit,
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

            conversionNote = QuantityNeedsReviewNote;
            _logger.LogWarning("Same-dimension conversion for UPC {upc} could not compute a ratio", item.Upc);
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
            {
                conversionNote = QuantityNeedsReviewNote;
                _logger.LogInformation("Cross-dimension conversion for UPC {upc}: no density found for '{name}', used default (water)", item.Upc, productName);
            }

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

            conversionNote = QuantityNeedsReviewNote;
            _logger.LogWarning("Cross-dimension conversion for UPC {upc} could not compute a ratio", item.Upc);
            return Math.Max(1, (int)Math.Ceiling(item.Quantity));
        }

        // internal (not private) so RecipeHelper.Tests can exercise these directly --
        // see InternalsVisibleTo in RecipeHelper.csproj.
        internal static bool AreSameDimension(MeasureDimension ingredientDim, PackDimension packDim)
        {
            // packDim is never PackDimension.Composite -- KrogerSizeParser now reports a
            // composite pack's real primary dimension (Weight/Volume/Unit) here, so a
            // composite pack only matches the dimension its primary measurement actually
            // is, instead of matching both volume and weight ingredients unconditionally.
            return (ingredientDim == MeasureDimension.Volume && packDim == PackDimension.Volume) ||
                   (ingredientDim == MeasureDimension.Weight && packDim == PackDimension.Weight);
        }

        internal static bool IsCrossDimension(MeasureDimension ingredientDim, PackDimension packDim)
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

                // Batch-fetch product details instead of one sequential Kroger call per
                // cart line -- same fix as ConvertIngredientsToCartItems already applies,
                // now reused here for the "View Cart" load.
                var items = cartItems.data[0].items;
                var productsByUpc = await GetProductsByUpcBatch(items.Select(i => i.upc), GetLocationId());
                foreach (var item in items)
                {
                    if (productsByUpc.TryGetValue(item.upc, out var productDetails))
                    {
                        products.Add(productDetails.ToDetailedCartItem(item.quantity));
                    }
                }

                return products;

            }
            catch (Exception ex)
            {
                // Exception type/message are put directly in the message template (not
                // just passed as the LogError exception argument) -- see the matching
                // comment in StorageService.StoreRecipeImage for why.
                _logger.LogError(ex, "Error getting Kroger cart items. ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    ex.GetType().FullName, ex.Message);
                return null;
            }
        }

    }
}
