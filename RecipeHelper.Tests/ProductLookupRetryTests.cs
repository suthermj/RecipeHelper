using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // A cart preview reported 17 ingredients as "Product lookup failed" at once.
    // GetProductsByUpcBatch gave up on a UPC after its first non-2xx response, so a
    // transient Kroger error (rate limiting on a burst of lookups, a 5xx) dropped the
    // item for good. These tests drive the lookup against a fake Kroger API.
    public class ProductLookupRetryTests
    {
        private const string TokenCacheKey = "kroger:client-credentials-token";

        private sealed class FakeKrogerHandler : HttpMessageHandler
        {
            private readonly Dictionary<string, Queue<HttpStatusCode>> _responses;
            public Dictionary<string, int> Calls { get; } = new();

            public FakeKrogerHandler(Dictionary<string, HttpStatusCode[]> responses) =>
                _responses = responses.ToDictionary(kv => kv.Key, kv => new Queue<HttpStatusCode>(kv.Value));

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var upc = request.RequestUri!.AbsolutePath.Split('/').Last();
                lock (Calls) Calls[upc] = Calls.GetValueOrDefault(upc) + 1;

                HttpStatusCode status;
                lock (_responses)
                {
                    var queue = _responses[upc];
                    status = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
                }

                var response = new HttpResponseMessage(status);
                if (status == HttpStatusCode.OK)
                {
                    response.Content = new StringContent(
                        "{\"data\":{\"productId\":\"" + upc + "\",\"upc\":\"" + upc + "\",\"description\":\"Product " + upc +
                        "\",\"brand\":\"Kroger\",\"items\":[{\"size\":\"16 oz\",\"soldBy\":\"UNIT\",\"price\":{\"regular\":1.99,\"promo\":0}}]}}");
                }
                return Task.FromResult(response);
            }
        }

        private sealed class FakeFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;
            public FakeFactory(HttpMessageHandler handler) => _handler = handler;
            public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false);
        }

        private static KrogerService BuildService(FakeKrogerHandler handler)
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Kroger:baseUri"] = "https://api.kroger.test/v1" })
                .Build();
            var cache = new MemoryCache(new MemoryCacheOptions());
            cache.Set(TokenCacheKey, "test-token"); // skip the token endpoint

            var factory = new FakeFactory(handler);
            var authService = new KrogerAuthService(db, factory, config,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(), NullLogger<KrogerAuthService>.Instance);

            return new KrogerService(factory, config, NullLogger<KrogerService>.Instance, authService, cache,
                new Microsoft.AspNetCore.Http.HttpContextAccessor())
            {
                LookupRetryBaseDelay = TimeSpan.FromMilliseconds(1),
            };
        }

        [Fact]
        public async Task TransientErrors_AreRetried_AndRecover()
        {
            var handler = new FakeKrogerHandler(new()
            {
                ["111"] = new[] { HttpStatusCode.TooManyRequests, HttpStatusCode.OK },
                ["222"] = new[] { HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway, HttpStatusCode.OK },
                ["333"] = new[] { HttpStatusCode.OK },
            });
            var service = BuildService(handler);

            var vm = new AddToCartVM
            {
                Items = new List<CartItemVM>
                {
                    new() { Name = "Milk", Upc = "111", Quantity = 1, Measurement = "Cups", Include = true },
                    new() { Name = "Heavy Cream", Upc = "222", Quantity = 1, Measurement = "Cups", Include = true },
                    new() { Name = "Beef broth", Upc = "333", Quantity = 1, Measurement = "Cups", Include = true },
                }
            };

            var result = await service.ConvertIngredientsToCartItems(vm);

            Assert.Empty(result.Skipped);
            Assert.Equal(3, result.Items.Count);
            Assert.Equal(2, handler.Calls["111"]);
            Assert.Equal(3, handler.Calls["222"]);
        }

        [Fact]
        public async Task PersistentFailure_IsSkipped_WithRetryData()
        {
            var handler = new FakeKrogerHandler(new()
            {
                ["111"] = new[] { HttpStatusCode.ServiceUnavailable },
                ["404"] = new[] { HttpStatusCode.NotFound },
            });
            var service = BuildService(handler);

            var vm = new AddToCartVM
            {
                Items = new List<CartItemVM>
                {
                    new() { Name = "Milk", Upc = "111", Quantity = 2, Measurement = "Cups", Include = true },
                    new() { Name = "Cornbread", Upc = "404", Quantity = 1, Measurement = "Unit", Include = true },
                }
            };

            var result = await service.ConvertIngredientsToCartItems(vm);

            Assert.Empty(result.Items);
            var milk = Assert.Single(result.Skipped, s => s.Name == "Milk");
            Assert.Equal(KrogerService.LookupFailedReason, milk.Reason);
            Assert.True(milk.Retryable);
            Assert.Equal("111", milk.Upc);
            Assert.Equal("Cups", milk.Measurement);
            Assert.Equal(2, milk.Quantity);
            Assert.Equal(3, handler.Calls["111"]); // retried up to the attempt limit

            var cornbread = Assert.Single(result.Skipped, s => s.Name == "Cornbread");
            Assert.Equal(KrogerService.ProductNotFoundReason, cornbread.Reason);
            Assert.False(cornbread.Retryable); // needs remapping, not a retry
            Assert.Equal(1, handler.Calls["404"]); // 404 isn't transient -- no retry
        }
    }
}
