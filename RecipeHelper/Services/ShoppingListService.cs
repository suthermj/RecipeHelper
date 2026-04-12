using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.Lists;

namespace RecipeHelper.Services
{
    public class ShoppingListService
    {
        private readonly ILogger<ShoppingListService> _logger;
        private readonly DatabaseContext _context;

        public ShoppingListService(ILogger<ShoppingListService> logger, DatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<List<ShoppingList>> GetAllAsync()
        {
            return await _context.ShoppingLists
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();
        }

        public async Task<ShoppingList?> GetByIdAsync(int id)
        {
            return await _context.ShoppingLists
                .Include(l => l.Items)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<ShoppingList> CreateAsync(string name, List<ShoppingListItem>? items = null)
        {
            var list = new ShoppingList
            {
                Name = name,
                CreatedDate = DateTime.UtcNow,
                Items = items ?? new()
            };

            _context.ShoppingLists.Add(list);
            await _context.SaveChangesAsync();
            return list;
        }

        public async Task<ShoppingListItem?> AddItemAsync(int listId, string name, int quantity = 1, string? upc = null)
        {
            var list = await _context.ShoppingLists.FindAsync(listId);
            if (list == null) return null;

            var item = new ShoppingListItem
            {
                ShoppingListId = listId,
                Name = name,
                Quantity = quantity,
                Upc = upc
            };

            _context.ShoppingListItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task<bool> RenameAsync(int listId, string name)
        {
            var list = await _context.ShoppingLists.FindAsync(listId);
            if (list == null) return false;

            list.Name = name;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateItemAsync(int itemId, int? quantity = null, bool? isCompleted = null)
        {
            var item = await _context.ShoppingListItems.FindAsync(itemId);
            if (item == null) return false;

            if (quantity.HasValue) item.Quantity = quantity.Value;
            if (isCompleted.HasValue) item.IsCompleted = isCompleted.Value;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveItemAsync(int itemId)
        {
            var item = await _context.ShoppingListItems.FindAsync(itemId);
            if (item == null) return false;

            _context.ShoppingListItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int listId)
        {
            var list = await _context.ShoppingLists
                .Include(l => l.Items)
                .FirstOrDefaultAsync(l => l.Id == listId);
            if (list == null) return false;

            _context.ShoppingLists.Remove(list);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
