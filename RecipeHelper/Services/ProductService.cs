using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Services
{
    public class ProductService
    {
        private DatabaseContext _context;
        private ILogger<ProductService> _logger;
        public ProductService(DatabaseContext context, ILogger<ProductService> logger)
        {
            _context = context;
            _logger = logger;
        }

        
        public async Task<List<KrogerProduct>> GetProductsAsync()
        {
            return await _context.KrogerProducts.Select(p => new KrogerProduct
            {
                Name = p.Name,
                Upc = p.Upc
            }).AsNoTracking()
            .ToListAsync();
        }
        public async Task<bool> AddProducts(List<KrogerDatabaseProduct> products)
        {

            if (products is null || products.Count == 0)
            {
                _logger.LogWarning("No products to add");
                return false;
            }
            else
            {
                foreach (var product in products)
                {
                    KrogerProduct newProduct = new KrogerProduct
                    {
                        Name = product.Name,
                        Upc = product.Upc
                    };
                    try
                    {
                        await _context.KrogerProducts.AddAsync(newProduct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding product {ProductName} with UPC {ProductUpc}", product.Name, product.Upc);
                        return false;
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Added products");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving products");
                    return false;
                }
            }
        }

        public async Task<List<ViewProductVM>> SearchForDbProduct(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                _logger.LogWarning("Search term required");
                return null;
            }

            var products = await _context.KrogerProducts.Where(p => p.Name.ToLower().Contains(term.ToLower()))
                            .OrderBy(p => p.Name)
                            .Select(p => new ViewProductVM { Name = p.Name, Upc = p.Upc })
                            .Take(5)
                            .ToListAsync();

            return products;
        }

        public async Task<KrogerProduct> GetProductAsync(string upc)
        {
            if (String.IsNullOrEmpty(upc))
            {
                _logger.LogWarning("Id required");
                return null;
            }

            _logger.LogInformation("Finding product by id [{productId}]", upc);
            var product = await _context.KrogerProducts
                .AsNoTracking()
                .Where(p => p.Upc == upc)
                .FirstOrDefaultAsync();

            return product;
        }
        /*
        public async Task<string?> GetProductUpcAsync(int id)
        {
            if (id == null)
            {
                _logger.LogWarning("Id required");
                return null;
            }

            _logger.LogInformation("Finding product by id [{productId}]", id);
            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new { upc = p.Upc })
                .FirstOrDefaultAsync();

            if (product is null)
            {
                _logger.LogError("Product not found by id [{productId}]", id);
                return null;
            }

            _logger.LogInformation("product id [{productId}] upc [{upc}]", id, product.upc);

            return product.upc;
        }*/
    }
}
