using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Account;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.Settings;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class SettingsController : Controller
    {
        private readonly KrogerService _krogerService;
        private readonly AccountService _accountService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(KrogerService krogerService, AccountService accountService, IConfiguration configuration, ILogger<SettingsController> logger)
        {
            _krogerService = krogerService;
            _accountService = accountService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentLocationId = Request.Cookies["KrogerLocationId"]
                ?? _configuration["Kroger:mariemontLocationId"]
                ?? "01400421";

            var currentStoreName = Request.Cookies["KrogerLocationName"] ?? "Mariemont (Default)";

            var vm = new SettingsVM
            {
                CurrentLocationId = currentLocationId,
                CurrentStoreName = currentStoreName
            };

            var householdId = User.GetHouseholdId();
            if (householdId != null)
            {
                var currentUserId = User.GetUserId();
                var (household, members) = await _accountService.GetHouseholdAsync(householdId.Value);
                vm.HouseholdName = household?.Name ?? "";
                vm.HouseholdMembers = members.Select(m => new HouseholdMemberVM
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName,
                    Email = m.Email,
                    IsCurrentUser = m.Id == currentUserId,
                }).ToList();
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SearchStores(string zipCode)
        {
            if (string.IsNullOrWhiteSpace(zipCode))
                return Json(new List<KrogerLocationDto>());

            var locations = await _krogerService.SearchLocations(zipCode);
            if (locations == null)
                _logger.LogWarning("SearchStores: Kroger location search returned null for ZipCode={ZipCode}.", zipCode);
            return Json(locations ?? new List<KrogerLocationDto>());
        }

        [HttpGet]
        public async Task<IActionResult> SearchStoresByLocation(double latitude, double longitude)
        {
            var locations = await _krogerService.SearchLocationsByLatLong(latitude, longitude);
            if (locations == null)
                _logger.LogWarning("SearchStoresByLocation: Kroger location search returned null for Latitude={Latitude}, Longitude={Longitude}.", latitude, longitude);
            return Json(locations ?? new List<KrogerLocationDto>());
        }

        [HttpPost]
        public IActionResult SelectStore(string locationId, string storeName, double? latitude, double? longitude)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            };

            Response.Cookies.Append("KrogerLocationId", locationId, cookieOptions);
            Response.Cookies.Append("KrogerLocationName", storeName, cookieOptions);

            if (latitude.HasValue && longitude.HasValue)
            {
                Response.Cookies.Append("KrogerLocationLat", latitude.Value.ToString(), cookieOptions);
                Response.Cookies.Append("KrogerLocationLng", longitude.Value.ToString(), cookieOptions);
            }

            _logger.LogInformation("Kroger store selected. LocationId={LocationId}, StoreName={StoreName}, HasCoordinates={HasCoordinates}",
                locationId, storeName, latitude.HasValue && longitude.HasValue);
            TempData["SuccessMessage"] = $"Store set to {storeName}";
            return RedirectToAction("Index");
        }
    }
}
