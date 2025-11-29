using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using Azure.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
        private DatabaseContext _context;
        private readonly string _baseUri;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _locationId;
        public KrogerService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<KrogerService> logger, KrogerAuthService krogerAuthService, DatabaseContext context)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["Kroger:baseUri"];
            _locationId = _configuration["Kroger:mariemontLocationId"];
            _clientId = _configuration["Kroger:clientId"];
            _clientSecret = _configuration["Kroger:clientSecret"];
            _krogerAuthService = krogerAuthService;
            _context = context;
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
                        stockLevel = krogerProduct.items.FirstOrDefault()?.inventory?.stockLevel ?? "N/A",
                        brand = krogerProduct.brand,
                        aisleLocation = krogerProduct.aisleLocations.FirstOrDefault()?.bayNumber ?? "N/A",
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

        public async Task<bool> AddToCart(AddToCartRequest vm, string accessToken)
        {
            var auth = await _krogerAuthService.EnsureAccessTokenAsync();

            if (!auth.IsAuthorized || string.IsNullOrEmpty(auth.AccessToken))
            {
                _logger.LogError("User not authorized for Kroger APIs. Prompting re-login.");


                return false;
            }

            if (vm == null || vm.Items.Count == 0)
            {
                _logger.LogError("AddToCartVM is null or has no items.");
                return false;
            }
            var client = _httpClientFactory.CreateClient();
            var url = $"{_baseUri}/cart/add";


            foreach (var item in vm.Items)
            {
                item.Quantity = (int)item.Quantity; // quantity is already int on VM, so update before use
            }


            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); // Replace with actual customer access token
                var jsonContent = JsonConvert.SerializeObject(vm);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await client.PutAsync(url, httpContent);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error adding item to cart.");
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

        public async Task<AddToCartRequest> ConvertIngredientsToCartItems(AddToCartVM vm)
        {
            AddToCartRequest response = new AddToCartRequest();
            List<CartItem> cartItems = new();

            foreach (var item in vm.Items)
            {
                var krogerProduct = await GetProductDetails(item.Upc);
                
                if (krogerProduct.soldBy.Equals("UNIT", StringComparison.OrdinalIgnoreCase))
                {
                    // ingredient measured in units (lasagna noodles / onions / ect)
                    if (item.Measurement.Equals("UNIT", StringComparison.OrdinalIgnoreCase))
                    {
                        cartItems.Add(new CartItem
                        {
                            Quantity = (int)Math.Ceiling(item.Quantity),
                            Upc = item.Upc
                        });
                    }
                    else
                    {
                        try
                        {
                            var krogerProductNormalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(krogerProduct.sizeUnit);
                            var ingredientNormalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(item.Measurement);
                            var krogerProductAmountSmallestUnit = MeasurementHelper.ConvertToLowestMeasurementUnit(krogerProductNormalizedMeasurementUnit, krogerProduct.sizeQuantity);
                            var ingredientAmountSmallestUnit = MeasurementHelper.ConvertToLowestMeasurementUnit(ingredientNormalizedMeasurementUnit, item.Quantity);

                            var finalQuantity = 1;

                            while (ingredientAmountSmallestUnit > krogerProductAmountSmallestUnit)
                            {
                                finalQuantity++;
                                krogerProductAmountSmallestUnit = krogerProductAmountSmallestUnit * finalQuantity;
                            }

                            cartItems.Add(new CartItem
                            {
                                Quantity = finalQuantity,
                                Upc = item.Upc
                            });
                        }
                        catch
                        {
                            _logger.LogInformation("Could not parse measurement " + item.Measurement + " for product " + item.Upc + ", skipping.");
                            continue;

                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Product " + krogerProduct.upc + " is not sold by unit, skipping for now.");
                }

            }

            response.Items = cartItems;
            return response;
        }

        public async Task<List<DetailedCartItem>> GetCurrentCartItems (string accessToken)
        {
            List<DetailedCartItem> products = new List<DetailedCartItem>();
            try
            {
                string url = $"{_baseUri}/carts";
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken); // Replace with actual customer access token
                
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Error getting cart items");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
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
                _logger.LogError("Error getting cart itesm");
                return null;
            }
        }


    }
}
