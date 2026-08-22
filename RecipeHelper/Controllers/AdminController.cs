using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace RecipeHelper.Controllers
{
    // Mobile-friendly viewer for `journalctl -u recipehelper` so prod logs can be
    // checked from a phone without SSH (the primary device for this app). There's
    // no user/identity system anywhere else in the app, so this is gated by a
    // single shared-secret token (AdminSettings:LogsToken in
    // appsettings.Production.json) rather than a login page -- visit once with
    // ?token=..., a long-lived cookie carries it after that so the plain URL can
    // be bookmarked / added to the home screen.
    public class AdminController : Controller
    {
        private const string TokenCookieName = "rh_admin_token";
        private const int DefaultLines = 300;
        private const int MaxLines = 2000;

        private readonly IConfiguration _config;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IConfiguration config, ILogger<AdminController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Logs(string? token)
        {
            if (!TryAuthorize(token, out var configuredToken))
                return NotFound();

            // Renew on every successful visit so the cookie never expires from
            // under a phone that's opened every so often.
            SetTokenCookie(configuredToken!);

            return View();
        }

        // Plain-text endpoint the Logs page polls via fetch(). Kept separate from
        // Logs() so the log content is never part of the page navigation itself --
        // sw.js caches page navigations (stale-while-revalidate), but a same-origin
        // fetch() call isn't a navigation, so it always hits the network fresh.
        [HttpGet]
        public async Task<IActionResult> LogsData(string? token, int lines = DefaultLines, string? grep = null)
        {
            if (!TryAuthorize(token, out _))
                return NotFound();

            lines = Math.Clamp(lines, 1, MaxLines);

            var psi = new ProcessStartInfo("journalctl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add("recipehelper");
            psi.ArgumentList.Add("-n");
            psi.ArgumentList.Add(lines.ToString());
            psi.ArgumentList.Add("--no-pager");
            psi.ArgumentList.Add("--output=short-iso");
            psi.ArgumentList.Add("--no-hostname");
            if (!string.IsNullOrWhiteSpace(grep))
            {
                psi.ArgumentList.Add("--grep");
                psi.ArgumentList.Add(grep);
            }

            using var proc = Process.Start(psi);
            if (proc == null)
                return Content("journalctl failed to start.", "text/plain");

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return Content("journalctl timed out.", "text/plain");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0 || (string.IsNullOrWhiteSpace(stdout) && !string.IsNullOrWhiteSpace(stderr)))
            {
                _logger.LogWarning("journalctl exited {Code}: {Stderr}", proc.ExitCode, stderr);
                var hint = stderr.Contains("permission", StringComparison.OrdinalIgnoreCase)
                    ? "\n\nHint: the app's service user needs journal read access -- on the VM run:\n  sudo usermod -aG systemd-journal www-data\n  sudo systemctl restart recipehelper"
                    : "";
                return Content($"{stdout}\n--- journalctl stderr (exit {proc.ExitCode}) ---\n{stderr}{hint}", "text/plain");
            }

            return Content(string.IsNullOrEmpty(stdout) ? "(no log lines)" : stdout, "text/plain");
        }

        private bool TryAuthorize(string? tokenFromQuery, out string? configuredToken)
        {
            configuredToken = _config["AdminSettings:LogsToken"];
            if (string.IsNullOrEmpty(configuredToken))
                return false; // fail closed until a token is actually configured

            var candidate = tokenFromQuery;
            if (string.IsNullOrEmpty(candidate))
                Request.Cookies.TryGetValue(TokenCookieName, out candidate);

            return ConstantTimeEquals(candidate, configuredToken);
        }

        private void SetTokenCookie(string token)
        {
            Response.Cookies.Append(TokenCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
            });
        }

        private static bool ConstantTimeEquals(string? a, string? b)
        {
            if (a == null || b == null) return false;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            if (aBytes.Length != bBytes.Length) return false;
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
