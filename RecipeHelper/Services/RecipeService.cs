using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NuGet.Protocol.Plugins;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;
using static System.Net.WebRequestMethods;

namespace RecipeHelper.Services
{
    public class RecipeService
    {
        private readonly ILogger<RecipeService> _logger;
        private DatabaseContext _context;
        private KrogerService _krogerService;
        private ProductService _productService;
        public RecipeService(ILogger<RecipeService> logger, DatabaseContext context, KrogerService krogerService, ProductService productService)
        {
            _context = context;
            _logger = logger;
            _krogerService = krogerService;
            _productService = productService;
        }

        
    }
}

