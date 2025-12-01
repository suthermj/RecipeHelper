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

        public async Task<Product> AddProduct(string name, string upc, decimal price)
        {
            try
            {
                Product product = new Product
                {
                    Name = name,
                    Price = price,
                    Upc = upc
                };

                await _context.Products.AddAsync(product);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added product {ProductName} with UPC {ProductUpc}", name, upc);

                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving product");
                return null;
            }
            
        }

        public async Task<bool> AddProducts(List<Models.Product> products)
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
                    try
                    {
                        await _context.Products.AddAsync(product);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error adding product {ProductName} with UPC {ProductUpc}", product.Name, product.Upc);
                        return false;
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
            return false;
        }

        public async Task<List<ViewProductVM>> SearchForDbProduct(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                _logger.LogWarning("Search term required");
                return null;
            }

            var products = await _context.Products.Where(p => p.Name.ToLower().Contains(term.ToLower()))
                            .OrderBy(p => p.Name)
                            .Select(p => new ViewProductVM { Id = p.Id, Name = p.Name, Upc = p.Upc })
                            .Take(5)
                            .ToListAsync();

            return products;
        }

        public async Task<Product> GetProductAsync(int id)
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
                .FirstOrDefaultAsync();

            return product;
        }

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
        }
    }
}
