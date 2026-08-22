using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace RecipeHelper.Controllers
{
    // Mobile-friendly viewer for `journalctl -u recipehelper` so prod logs can be
    // checked from a phone without SSH (the primary device for this app).
    //
    // NOTE: left open (no auth) for now, at the user's request -- this app has no
    // user/identity system anywhere else, and the route isn't linked from anywhere
    // public. If that changes, gate this the way /Admin/Logs was originally built
    // (see git history around the AdminSettings:LogsToken shared-secret + cookie
    // approach) before relying on obscurity alone.
    public class AdminController : Controller
    {
        private const int DefaultLines = 300;
        private const int MaxLines = 2000;

        private readonly ILogger<AdminController> _logger;

        public AdminController(ILogger<AdminController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Logs()
        {
            return View();
        }

        // Plain-text endpoint the Logs page polls via fetch(). Kept separate from
        // Logs() so the log content is never part of the page navigation itself --
        // sw.js caches page navigations (stale-while-revalidate), but a same-origin
        // fetch() call isn't a navigation, so it always hits the network fresh.
        [HttpGet]
        public async Task<IActionResult> LogsData(int lines = DefaultLines, string? grep = null)
        {
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
    }
}
