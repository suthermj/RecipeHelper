using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using RecipeHelper.Models.Account;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    // App login (who you are in RecipeHelper). Not to be confused with AuthController,
    // which is the Kroger OAuth flow for cart access.
    public class AccountController : Controller
    {
        public const string RateLimitPolicy = "account-auth";

        private readonly AccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(AccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        // Login/setup/join pages must never land in the service worker's page cache:
        // a cached login form would be replayed after signing in, and its antiforgery
        // token would be stale.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Response.Headers["X-SW-No-Cache"] = "1";
            base.OnActionExecuting(context);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated == true) return LocalRedirectOrHome(returnUrl);
            if (!await _accountService.AnyUsersAsync()) return RedirectToAction(nameof(Setup));

            return View(new LoginVM { ReturnUrl = returnUrl });
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicy)]
        public async Task<IActionResult> Login(LoginVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _accountService.ValidateCredentialsAsync(vm.Email, vm.Password);
            if (user == null)
            {
                _logger.LogWarning("Failed login attempt.");
                ModelState.AddModelError("", "That email and password don't match.");
                vm.Password = "";
                return View(vm);
            }

            await SignInAsync(user);
            _logger.LogInformation("User signed in. UserId={UserId}", user.Id);
            return LocalRedirectOrHome(vm.ReturnUrl);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Setup()
        {
            if (await _accountService.AnyUsersAsync()) return RedirectToAction(nameof(Login));
            return View(new SetupVM());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicy)]
        public async Task<IActionResult> Setup(SetupVM vm)
        {
            if (await _accountService.AnyUsersAsync()) return RedirectToAction(nameof(Login));
            if (!ModelState.IsValid) return View(vm);

            var user = await _accountService.CreateFirstHouseholdAsync(vm.HouseholdName, vm.DisplayName, vm.Email, vm.Password);
            if (user == null) return RedirectToAction(nameof(Login));

            await SignInAsync(user);
            TempData["SuccessMessage"] = $"Welcome, {user.DisplayName}!";
            return LocalRedirectOrHome(null);
        }

        [AllowAnonymous]
        [HttpGet("Account/Join/{token}")]
        public async Task<IActionResult> Join(string token)
        {
            var invite = await _accountService.GetValidInviteAsync(token);
            if (invite == null) return View("InviteInvalid");

            return View(new JoinVM
            {
                Token = token,
                HouseholdName = invite.Household.Name,
                InvitedBy = invite.CreatedByUser.DisplayName,
            });
        }

        [AllowAnonymous]
        [HttpPost("Account/Join/{token}")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicy)]
        public async Task<IActionResult> Join(string token, JoinVM vm)
        {
            var invite = await _accountService.GetValidInviteAsync(token);
            if (invite == null) return View("InviteInvalid");

            vm.Token = token;
            vm.HouseholdName = invite.Household.Name;
            vm.InvitedBy = invite.CreatedByUser.DisplayName;

            if (ModelState.IsValid && await _accountService.EmailInUseAsync(vm.Email))
                ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists. Sign in instead.");
            if (!ModelState.IsValid) return View(vm);

            var user = await _accountService.AcceptInviteAsync(token, vm.DisplayName, vm.Email, vm.Password);
            if (user == null) return View("InviteInvalid");

            await SignInAsync(user);
            TempData["SuccessMessage"] = $"Welcome to {vm.HouseholdName}, {user.DisplayName}!";
            return LocalRedirectOrHome(null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        // Called from Settings -> Household -> "Invite someone". Returns the link for
        // the native share sheet.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInvite()
        {
            var userId = User.GetUserId();
            var householdId = User.GetHouseholdId();
            if (userId == null || householdId == null) return Unauthorized();

            var token = await _accountService.CreateInviteAsync(householdId.Value, userId.Value);
            var url = Url.Action(nameof(Join), "Account", new { token }, Request.Scheme);
            return Json(new { url, expiresInDays = (int)AccountRules.InviteLifetime.TotalDays });
        }

        private Task SignInAsync(AppUser user) =>
            HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                AccountService.CreatePrincipal(user),
                // Always persistent: this is a home-screen app, "remember me" is the
                // only sensible behavior. Lifetime/sliding renewal is set in Program.cs.
                new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });

        private IActionResult LocalRedirectOrHome(string? returnUrl) =>
            !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : LocalRedirect("/");
    }
}
