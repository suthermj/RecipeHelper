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
    private readonly ILogger<KrogerAuthService> _logger;

    public KrogerAuthService(
        DatabaseContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IHttpContextAccessor httpContextAccessor,
        ILogger<KrogerAuthService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> GetKrogerAccessTokenAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext
                         ?? throw new InvalidOperationException("No HttpContext");

        var krogerProfileId = httpContext.Request.Cookies["KrogerProfileId"];

        // If we've never connected Kroger for this browser
        if (string.IsNullOrEmpty(krogerProfileId))
        {
            _logger.LogInformation("GetKrogerAccessTokenAsync: no KrogerProfileId cookie present.");
            return null;
        }

        var token = await _db.KrogerCustomerTokens
            .SingleOrDefaultAsync(t => t.KrogerProfileId == krogerProfileId);

        if (token == null)
        {
            _logger.LogWarning("GetKrogerAccessTokenAsync: no stored token for KrogerProfileId={KrogerProfileId} (cookie present but no matching DB row).", krogerProfileId);
            return null;
        }

        // If token is still valid, just return it
        if (token.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return token.AccessToken;
        }

        _logger.LogInformation("GetKrogerAccessTokenAsync: stored token for KrogerProfileId={KrogerProfileId} is expired. ExpiresAtUtc={ExpiresAtUtc}", krogerProfileId, token.AccessTokenExpiresAtUtc);
        return null;
    }

    public async Task<KrogerAuthResult> EnsureAccessTokenAsync(string? returnUrl = null)
    {
        var httpContext = _httpContextAccessor.HttpContext
                         ?? throw new InvalidOperationException("No HttpContext");

        var krogerProfileId = httpContext.Request.Cookies["KrogerProfileId"];

        // If we've never connected Kroger for this browser
        if (string.IsNullOrEmpty(krogerProfileId))
        {
            _logger.LogInformation("EnsureAccessTokenAsync: no KrogerProfileId cookie present, redirecting to login. ReturnUrl={ReturnUrl}", returnUrl);
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
            _logger.LogWarning("EnsureAccessTokenAsync: no stored token for KrogerProfileId={KrogerProfileId} (cookie present but no matching DB row), redirecting to login.", krogerProfileId);
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
        _logger.LogInformation("EnsureAccessTokenAsync: token expired for KrogerProfileId={KrogerProfileId}, attempting refresh. ExpiresAtUtc={ExpiresAtUtc}", krogerProfileId, token.AccessTokenExpiresAtUtc);
        var refreshed = await RefreshAccessTokenAsync(token);
        if (refreshed == null)
        {
            // refresh failed → force re-auth
            _logger.LogWarning("EnsureAccessTokenAsync: refresh failed for KrogerProfileId={KrogerProfileId}, forcing re-auth.", krogerProfileId);
            return new KrogerAuthResult
            {
                IsAuthorized = false,
                RedirectUrl = BuildLoginRedirectUrl(returnUrl)
            };
        }

        _logger.LogInformation("EnsureAccessTokenAsync: refresh succeeded for KrogerProfileId={KrogerProfileId}.", krogerProfileId);
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

        // Response body is not logged even on failure -- Kroger's token endpoint can
        // echo request fields back in error payloads, and this request includes the
        // client secret and refresh token.
        var response = await client.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form));
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant etc. – treat as not authorized
            _logger.LogWarning("Kroger token refresh failed for KrogerProfileId={KrogerProfileId}. StatusCode={StatusCode}", token.KrogerProfileId, (int)response.StatusCode);
            return null;
        }

        var refreshed = JsonSerializer.Deserialize<TokenResponse>(json)!;

        if (string.IsNullOrEmpty(refreshed.Token) || string.IsNullOrEmpty(refreshed.RefreshToken))
        {
            _logger.LogWarning("Kroger token refresh for KrogerProfileId={KrogerProfileId} returned 2xx but was missing an access or refresh token.", token.KrogerProfileId);
            return null;
        }

        token.AccessToken = refreshed.Token;
        token.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn);

        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
        {
            token.RefreshToken = refreshed.RefreshToken;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Kroger token refreshed for KrogerProfileId={KrogerProfileId}. NewExpiresAtUtc={NewExpiresAtUtc}", token.KrogerProfileId, token.AccessTokenExpiresAtUtc);
        return new KrogerAuthResult
        {
            IsAuthorized = true,
            KrogerProfileId = token.KrogerProfileId,
            AccessToken = token.AccessToken
        };
    }
}
