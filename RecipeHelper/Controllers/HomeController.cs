using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models;

namespace RecipeHelper.Controllers
{
    // Target of app.UseExceptionHandler("/Home/Error") in Program.cs -- reached
    // whenever an unhandled exception occurs in production.
    public class HomeController : Controller
    {
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
