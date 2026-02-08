using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class IngredientsController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly KrogerService _krogerService;
        private readonly IngredientsService _ingredientsService;
        private readonly ILogger<IngredientsController> _logger;

        public IngredientsController(DatabaseContext context, KrogerService krogerService, ILogger<IngredientsController> logger, IngredientsService ingredientsService)
        {
            _context = context;
            _krogerService = krogerService;
            _logger = logger;
            _ingredientsService = ingredientsService;
        }
        /*public async Task<ActionResult> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return Json(new List<IngredientSearchResponse>());
            }

            return Json(await _ingredientsService.Search(searchTerm));
        }*/

       /* [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([FromBody] CreateIngredientRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Ingredient name cannot be empty.");
            }

            return Json(await _ingredientsService.Create(request.Name));
        }*/
    }
}
