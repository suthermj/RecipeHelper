using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace RecipeHelper.Tests
{
    // Boots the real app (Program.cs middleware, cookie auth, fallback authorization
    // policy, rate limiter, Razor views) in-process against an in-memory database, and
    // drives it over HTTP the way a browser would.
    public class AuthPipelineTests : IDisposable
    {
        private readonly InMemoryDatabaseRoot _dbRoot = new();
        private readonly string _dbName = Guid.NewGuid().ToString();
        private readonly string _keysDir = Path.Combine(Path.GetTempPath(), "rh-keys-" + Guid.NewGuid());
        private readonly List<IDisposable> _disposables = new();

        public void Dispose()
        {
            foreach (var d in _disposables) d.Dispose();
            if (Directory.Exists(_keysDir)) Directory.Delete(_keysDir, recursive: true);
        }

        private sealed class AppFactory : WebApplicationFactory<Program>
        {
            private readonly InMemoryDatabaseRoot _dbRoot;
            private readonly string _dbName;
            private readonly string _keysDir;

            public AppFactory(InMemoryDatabaseRoot dbRoot, string dbName, string keysDir)
            {
                _dbRoot = dbRoot;
                _dbName = dbName;
                _keysDir = keysDir;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionString", "Server=unused-in-tests");
                builder.UseSetting("OpenAI:ApiKey", "unused-in-tests");
                // StorageService (a RecipeController dependency) validates these at
                // construction; nothing in these tests touches blob storage.
                builder.UseSetting("StorageSettings:accountUri", "https://unused.blob.core.windows.net");
                builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-000000000000");
                builder.UseSetting("AzureAd:ClientId", "unused");
                builder.UseSetting("AzureAd:ClientSecret", "unused");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<DatabaseContext>>();
                    services.RemoveAll<DbContextOptions>();
                    services.RemoveAll(typeof(IDbContextOptionsConfiguration<DatabaseContext>));
                    services.AddDbContext<DatabaseContext>(o => o.UseInMemoryDatabase(_dbName, _dbRoot));

                    // Stand-in for /var/lib/recipehelper/keys: a directory that outlives
                    // any one app instance, like the real one does across deploys.
                    services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(_keysDir));
                });
            }
        }

        private AppFactory StartApp(string? keysDir = null)
        {
            var factory = new AppFactory(_dbRoot, _dbName, keysDir ?? _keysDir);
            _disposables.Add(factory);
            return factory;
        }

        private static HttpClient Browser(AppFactory app) =>
            app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        private static async Task<string> AntiforgeryTokenFrom(HttpClient client, string url)
        {
            var html = await client.GetStringAsync(url);
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
            Assert.True(match.Success, $"No antiforgery token on {url}");
            return match.Groups[1].Value;
        }

        private static async Task<HttpResponseMessage> PostForm(HttpClient client, string formPageUrl, string postUrl, Dictionary<string, string> fields)
        {
            fields["__RequestVerificationToken"] = await AntiforgeryTokenFrom(client, formPageUrl);
            return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
        }

        private static Task<HttpResponseMessage> SetUp(HttpClient client) =>
            PostForm(client, "/Account/Setup", "/Account/Setup", new()
            {
                ["HouseholdName"] = "Test Home",
                ["DisplayName"] = "Alex",
                ["Email"] = "alex@example.com",
                ["Password"] = "password123",
            });

        private static string AuthCookieFrom(HttpResponseMessage response) =>
            response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("RecipeHelper.Auth="));

        [Theory]
        [InlineData(null)]
        [InlineData("navigate")]
        public async Task SignedOutPageNavigation_RedirectsToLogin_WithReturnUrl(string? fetchMode)
        {
            var client = Browser(StartApp());
            var request = new HttpRequestMessage(HttpMethod.Get, "/Dinner/Index");
            if (fetchMode != null) request.Headers.Add("Sec-Fetch-Mode", fetchMode);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login?ReturnUrl=%2FDinner%2FIndex", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task SignedOutFetch_Gets401_NotLoginPageHtml()
        {
            var client = Browser(StartApp());
            var request = new HttpRequestMessage(HttpMethod.Get, "/Settings/SearchStores?zipCode=45227");
            request.Headers.Add("Sec-Fetch-Mode", "cors");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("/css/output.css")]
        [InlineData("/sw.js")]
        [InlineData("/manifest.json")]
        [InlineData("/offline.html")]
        public async Task StaticFiles_StayPublic(string path)
        {
            var response = await Browser(StartApp()).GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ShareLinks_StayPublic()
        {
            // Unknown token: 404 from the share action itself, not a login redirect.
            var response = await Browser(StartApp()).GetAsync("/Share/MealPlan/no-such-token");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private static async Task<int> SeedRecipe(AppFactory app, string name)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var recipe = new Models.Recipe
            {
                Name = name,
                DinnerCategory = "Chicken",
                Instructions = "[\"Preheat the oven.\",\"Roast the chicken.\"]",
            };
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return recipe.Id;
        }

        [Fact]
        public async Task SignedOut_CanBrowseRecipes_ReadOnly()
        {
            var app = StartApp();
            var id = await SeedRecipe(app, "Lemon Roast Chicken");
            var visitor = Browser(app);

            var list = await visitor.GetAsync("/Recipe");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var listHtml = await list.Content.ReadAsStringAsync();
            Assert.Contains("Lemon Roast Chicken", listHtml);
            Assert.Contains("/Account/Login", listHtml);   // Sign in link
            Assert.DoesNotContain("ios-tab", listHtml);    // no tab bar / + button

            var view = await visitor.GetAsync($"/Recipe/ViewRecipe/{id}");
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            var viewHtml = await view.Content.ReadAsStringAsync();
            Assert.Contains("Lemon Roast Chicken", viewHtml);
            Assert.Contains("Roast the chicken.", viewHtml);
            Assert.DoesNotContain("CreateEditRecipe", viewHtml);
            Assert.DoesNotContain("Delete recipe", viewHtml);
            Assert.DoesNotContain("Add to Cart", viewHtml);
        }

        [Fact]
        public async Task SignedOut_CannotCreateEditOrDeleteRecipes()
        {
            var app = StartApp();
            var id = await SeedRecipe(app, "Lemon Roast Chicken");
            var visitor = Browser(app);

            Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Recipe/CreateEditRecipe")).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync($"/Recipe/CreateEditRecipe/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await visitor.GetAsync("/Import/ImportRecipe")).StatusCode);

            // DeleteRecipe is routed at POST /{id}. Signed out, it must bounce to login
            // before ever reaching the action.
            var delete = await visitor.PostAsync($"/{id}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
            Assert.Contains("/Account/Login", delete.Headers.Location!.ToString());

            using var scope = app.Services.CreateScope();
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<DatabaseContext>().Recipes.FindAsync(id));
        }

        [Fact]
        public async Task SignedIn_SeesRecipeEditActions()
        {
            var app = StartApp();
            var id = await SeedRecipe(app, "Lemon Roast Chicken");
            var client = Browser(app);
            await SetUp(client);

            var viewHtml = await client.GetStringAsync($"/Recipe/ViewRecipe/{id}");
            Assert.Contains("CreateEditRecipe", viewHtml);
            Assert.Contains("Delete recipe", viewHtml);
            Assert.Contains("ios-tab", viewHtml);
        }

        [Fact]
        public async Task FirstRun_LoginRedirectsToSetup_ThenSetupClosesAfterFirstAccount()
        {
            var app = StartApp();
            var client = Browser(app);

            var login = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.EndsWith("/Account/Setup", login.Headers.Location!.ToString());

            var setupPage = await client.GetAsync("/Account/Setup");
            Assert.Equal(HttpStatusCode.OK, setupPage.StatusCode);
            Assert.Equal("1", setupPage.Headers.GetValues("X-SW-No-Cache").Single());
            Assert.DoesNotContain("ios-tab", await setupPage.Content.ReadAsStringAsync());

            var created = await SetUp(client);
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

            // A second visitor can't run setup again.
            var stranger = Browser(app);
            var again = await stranger.GetAsync("/Account/Setup");
            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.EndsWith("/Account/Login", again.Headers.Location!.ToString());
        }

        [Fact]
        public async Task SignIn_IssuesPersistentCookie_ForAboutAYear()
        {
            var response = await SetUp(Browser(StartApp()));

            var cookie = AuthCookieFrom(response);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

            var expires = Regex.Match(cookie, "expires=([^;]+)", RegexOptions.IgnoreCase);
            Assert.True(expires.Success, "Auth cookie must be persistent (have an expiry), not a session cookie: " + cookie);
            var lifetime = DateTimeOffset.Parse(expires.Groups[1].Value) - DateTimeOffset.UtcNow;
            Assert.InRange(lifetime.TotalDays, 364, 366);
        }

        // The concern this whole design answers: does a deploy (process restart) sign
        // everyone out? It doesn't as long as the data protection keys persist, and it
        // does if they don't -- the second half proves the first isn't a fluke.
        [Fact]
        public async Task AuthCookie_SurvivesAppRestart_WhenKeysPersist()
        {
            var firstInstance = StartApp();
            var cookie = AuthCookieFrom(await SetUp(Browser(firstInstance)));
            var cookiePair = cookie.Split(';')[0];
            firstInstance.Dispose();

            var afterDeploy = Browser(StartApp());
            var request = new HttpRequestMessage(HttpMethod.Get, "/Settings/Index");
            request.Headers.Add("Cookie", cookiePair);
            var response = await afterDeploy.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Test Home", await response.Content.ReadAsStringAsync());

            var lostKeysDir = Path.Combine(Path.GetTempPath(), "rh-keys-" + Guid.NewGuid());
            try
            {
                var withLostKeys = Browser(StartApp(lostKeysDir));
                var request2 = new HttpRequestMessage(HttpMethod.Get, "/Settings/Index");
                request2.Headers.Add("Cookie", cookiePair);
                var response2 = await withLostKeys.SendAsync(request2);
                Assert.Equal(HttpStatusCode.Redirect, response2.StatusCode);
            }
            finally
            {
                if (Directory.Exists(lostKeysDir)) Directory.Delete(lostKeysDir, recursive: true);
            }
        }

        [Fact]
        public async Task InviteLink_AddsSecondMemberToSameHousehold()
        {
            var app = StartApp();
            var owner = Browser(app);
            await SetUp(owner);

            var settingsHtml = await owner.GetStringAsync("/Settings/Index");
            Assert.Contains("Invite someone", settingsHtml);
            Assert.Contains("ios-tab", settingsHtml); // signed in: tab bar is back

            var token = await AntiforgeryTokenFrom(owner, "/Settings/Index");
            var inviteRequest = new HttpRequestMessage(HttpMethod.Post, "/Account/CreateInvite");
            inviteRequest.Headers.Add("RequestVerificationToken", token);
            var inviteResponse = await owner.SendAsync(inviteRequest);
            Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
            var inviteUrl = JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync())
                .RootElement.GetProperty("url").GetString()!;
            var invitePath = new Uri(inviteUrl).AbsolutePath;
            Assert.StartsWith("/Account/Join/", invitePath);

            var partner = Browser(app);
            var joinPage = await partner.GetStringAsync(invitePath);
            Assert.Contains("Join Test Home", joinPage);
            Assert.Contains("Alex invited you", joinPage);

            var joined = await PostForm(partner, invitePath, invitePath, new()
            {
                ["DisplayName"] = "Sam",
                ["Email"] = "sam@example.com",
                ["Password"] = "password456",
            });
            Assert.Equal(HttpStatusCode.Redirect, joined.StatusCode);

            var partnerSettings = await partner.GetStringAsync("/Settings/Index");
            Assert.Contains("Test Home", partnerSettings);
            Assert.Contains("alex@example.com", partnerSettings);
            Assert.Contains("sam@example.com", partnerSettings);

            // Link is single-use.
            var reuse = await Browser(app).GetStringAsync(invitePath);
            Assert.Contains("Invite link expired", reuse);
        }

        [Fact]
        public async Task SignOut_ThenSignIn_WithPassword()
        {
            var app = StartApp();
            var client = Browser(app);
            await SetUp(client);

            var signedOut = await PostForm(client, "/Settings/Index", "/Account/Logout", new());
            Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Dinner/Index")).StatusCode);

            var wrong = await PostForm(client, "/Account/Login", "/Account/Login", new()
            {
                ["Email"] = "alex@example.com",
                ["Password"] = "not-it",
            });
            Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
            Assert.Contains("don&#x27;t match", await wrong.Content.ReadAsStringAsync());

            var right = await PostForm(client, "/Account/Login?ReturnUrl=%2FSettings%2FIndex", "/Account/Login", new()
            {
                ["Email"] = "ALEX@example.com",
                ["Password"] = "password123",
                ["ReturnUrl"] = "/Settings/Index",
            });
            Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
            Assert.Equal("/Settings/Index", right.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Settings/Index")).StatusCode);
        }

        [Fact]
        public async Task Login_RejectsOffSiteReturnUrl()
        {
            var client = Browser(StartApp());
            await SetUp(client);
            await PostForm(client, "/Settings/Index", "/Account/Logout", new());

            var response = await PostForm(client, "/Account/Login", "/Account/Login", new()
            {
                ["Email"] = "alex@example.com",
                ["Password"] = "password123",
                ["ReturnUrl"] = "https://evil.example.com/",
            });
            Assert.Equal("/", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task LoginAttempts_AreRateLimited()
        {
            var client = Browser(StartApp());
            await SetUp(client);
            await PostForm(client, "/Settings/Index", "/Account/Logout", new());

            HttpResponseMessage? last = null;
            for (var i = 0; i < 11; i++)
            {
                last = await PostForm(client, "/Account/Login", "/Account/Login", new()
                {
                    ["Email"] = "alex@example.com",
                    ["Password"] = "guess-" + i,
                });
                if (i < 9) Assert.Equal(HttpStatusCode.OK, last.StatusCode);
            }
            // Setup's POST above already used one of the 10 permits in this window.
            Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        }
    }
}
