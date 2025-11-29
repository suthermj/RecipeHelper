using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Web;
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NuGet.Common;
using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Controllers
{
    public class AuthController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;
        private DatabaseContext _context;

        public AuthController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AuthController> logger, DatabaseContext context)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
            _context = context;
        }

        // STEP 1: Redirect the user to the authorization endpoint
        [HttpGet("auth/login")]
        public IActionResult Login(string? returnUrl = "/")
        {
            var clientId = _config["OAuth:ClientId"];
            var redirectUri = HttpUtility.UrlEncode(_config["OAuth:RedirectUri"]);
            var state = HttpUtility.UrlEncode(returnUrl);
            var authUrl = $"{_config["OAuth:AuthorizeUrl"]}" +
                          $"?client_id={_config["OAuth:ClientId"]}" +
                          $"&response_type=code" +
                          $"&redirect_uri={_config["OAuth:RedirectUri"]}" +
                          $"&scope=cart.basic:write profile.compact" +
                          $"&state={state}";

            return Redirect(authUrl);
        }

        // STEP 2: This route receives the ?code=123 after login succeeds
        [HttpGet("auth/callback")]
        public async Task<IActionResult> Callback(string code, string state, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return Content($"OAuth Error: {error}");
            }

            if (string.IsNullOrEmpty(code))
            {
                return Content("No authorization code received.");
            }

            // Exchange the code for tokens
            var client = _httpClientFactory.CreateClient();
            var tokenEndpoint = _config["OAuth:TokenUrl"];

            var data = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", _config["OAuth:RedirectUri"] },
                { "client_id", _config["OAuth:ClientId"] },
                { "client_secret", _config["OAuth:ClientSecret"] }
            };

            var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(data));

            if (!response.IsSuccessStatusCode)
            {
                return Content("Error retrieving tokens from authorization code.");
            }

            var body = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(body);

            //return Content("Token Response:\n\n" + token.Token);

            client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.kroger.com");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

            var profileResponse = await client.GetAsync("/v1/identity/profile");
            var currentUser = JsonConvert.DeserializeObject<ProfileResponse>(await profileResponse.Content.ReadAsStringAsync());

            if (currentUser == null)
            {
                _logger.LogError("Kroger profile not found");
                return null;
            }

            var currentUserId = currentUser.data.id;
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            var existing = await _context.KrogerCustomerTokens.SingleOrDefaultAsync(t => t.KrogerProfileId == currentUserId);

            if (existing == null)
            {
                existing = new KrogerCustomerToken
                {
                    KrogerProfileId = currentUserId,
                    AccessToken = tokenResponse.Token,
                    RefreshToken = tokenResponse.RefreshToken,
                    AccessTokenExpiresAtUtc = expiresAt,
                };

                await _context.KrogerCustomerTokens.AddAsync(existing);
            }
            else
            {
                existing.AccessTokenExpiresAtUtc = expiresAt;
                existing.RefreshToken = tokenResponse.RefreshToken;
                existing.AccessToken = tokenResponse.Token;
            }

            await _context.SaveChangesAsync();

            Response.Cookies.Append(
                "KrogerProfileId",
                currentUserId,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                }
            );

            var returnUrl = "/Recipe";
            if (!string.IsNullOrEmpty(state))
                returnUrl = state;

            return LocalRedirect(returnUrl);
        }


    }
}