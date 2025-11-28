using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RecipeHelper;
using RecipeHelper.Models.Kroger;

public class KrogerAuthService
{
    private readonly DatabaseContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public KrogerAuthService(
        DatabaseContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<KrogerAuthResult> EnsureAccessTokenAsync(string? returnUrl = null)
    {
        var httpContext = _httpContextAccessor.HttpContext
                         ?? throw new InvalidOperationException("No HttpContext");

        var krogerProfileId = httpContext.Request.Cookies["KrogerProfileId"];

        // If we've never connected Kroger for this browser
        if (string.IsNullOrEmpty(krogerProfileId))
        {
            return new KrogerAuthResult
            {
                IsAuthorized = false,
                RedirectUrl = BuildLoginRedirectUrl(returnUrl)
            };
        }

        var token = await _db.KrogerCustomerTokens
            .SingleOrDefaultAsync(t => t.KrogerProfileId == krogerProfileId);

        if (token == null)
        {
            return new KrogerAuthResult
            {
                IsAuthorized = false,
                RedirectUrl = BuildLoginRedirectUrl(returnUrl)
            };
        }

        // If token is still valid, just return it
        if (token.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return new KrogerAuthResult
            {
                IsAuthorized = true,
                KrogerProfileId = krogerProfileId,
                AccessToken = token.AccessToken
            };
        }

        // Token expired → try refresh
        var refreshed = await RefreshAccessTokenAsync(token);
        if (refreshed == null)
        {
            // refresh failed → force re-auth
            return new KrogerAuthResult
            {
                IsAuthorized = false,
                RedirectUrl = BuildLoginRedirectUrl(returnUrl)
            };
        }

        return new KrogerAuthResult
        {
            IsAuthorized = true,
            KrogerProfileId = krogerProfileId,
            AccessToken = refreshed.AccessToken
        };
    }

    private string BuildLoginRedirectUrl(string? returnUrl)
    {
        // relative path & query from current request if none supplied
        if (string.IsNullOrEmpty(returnUrl))
        {
            var ctx = _httpContextAccessor.HttpContext!;
            returnUrl = ctx.Request.Path + ctx.Request.QueryString;
        }

        var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
        return $"/auth/login?returnUrl={encodedReturnUrl}";
    }

    private async Task<KrogerAuthResult?> RefreshAccessTokenAsync(KrogerCustomerToken token)
    {
        var client = _httpClientFactory.CreateClient();
        var tokenEndpoint = _config["OAuth:TokenUrl"];

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = token.RefreshToken,
            ["client_id"] = _config["OAuth:ClientId"],
            ["client_secret"] = _config["OAuth:ClientSecret"],
        };

        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form));
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant etc. – treat as not authorized
            return null;
        }

        var refreshed = JsonSerializer.Deserialize<TokenResponse>(json)!;

        token.AccessToken = refreshed.Token;
        token.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);

        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
        {
            token.RefreshToken = refreshed.RefreshToken;
        }

        await _db.SaveChangesAsync();

        return new KrogerAuthResult
        {
            IsAuthorized = true,
            KrogerProfileId = token.KrogerProfileId,
            AccessToken = token.AccessToken
        };
    }
}
