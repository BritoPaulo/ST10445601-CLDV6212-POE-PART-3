using Microsoft.EntityFrameworkCore;
using ABCRetailers.Data;
using ABCRetailers.Models;
using ABCRetailers.Models.SQL;
using ABCRetailers.Models.ViewModels;

namespace ABCRetailers.Services
{
    public interface ICartService
    {
        Task<List<CartItemViewModel>> GetCartItemsAsync(Guid userId);
        Task AddToCartAsync(Guid userId, string productId, int quantity = 1);
        Task UpdateCartItemAsync(Guid cartId, int quantity);
        Task RemoveFromCartAsync(Guid cartId);
        Task ClearCartAsync(Guid userId);
        Task<int> GetCartItemCountAsync(Guid userId);
    }

    public class CartService : ICartService
    {
        private readonly AuthDbContext _context;
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<CartService> _logger;

        public CartService(AuthDbContext context, IAzureStorageService storageService, ILogger<CartService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<List<CartItemViewModel>> GetCartItemsAsync(Guid userId)
        {
            try
            {
                var cartItems = await _context.ShoppingCart
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                var viewModels = new List<CartItemViewModel>();

                foreach (var item in cartItems)
                {
                    var product = await _storageService.GetEntityAsync<Product>("Product", item.ProductId);
                    if (product != null)
                    {
                        viewModels.Add(new CartItemViewModel
                        {
                            CartId = item.CartId,
                            ProductId = item.ProductId,
                            ProductName = product.ProductName,
                            ImageUrl = product.ImageUrl,
                            Price = product.Price,
                            Quantity = item.Quantity,
                            StockAvailable = product.StockAvailable
                        });
                    }
                }

                return viewModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart items for user: {UserId}", userId);
                return new List<CartItemViewModel>();
            }
        }

        public async Task AddToCartAsync(Guid userId, string productId, int quantity = 1)
        {
            try
            {
                _logger.LogInformation("🛒 Adding product {ProductId} to cart for user {UserId}", productId, userId);

                var existingItem = await _context.ShoppingCart
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

                if (existingItem != null)
                {
                    // Update existing item
                    existingItem.Quantity += quantity;
                    _logger.LogInformation("📈 Updated existing cart item");
                }
                else
                {
                    // Create new cart item
                    var cartItem = new SqlCart
                    {
                        CartId = Guid.NewGuid(),
                        UserId = userId,  // Simple assignment, no navigation
                        ProductId = productId,
                        Quantity = quantity,
                        AddedDate = DateTime.UtcNow
                    };
                    _context.ShoppingCart.Add(cartItem);
                    _logger.LogInformation("🆕 Created new cart item");
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Successfully added to cart");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in AddToCartAsync");
                throw;
            }
        }

        public async Task UpdateCartItemAsync(Guid cartId, int quantity)
        {
            try
            {
                var cartItem = await _context.ShoppingCart.FindAsync(cartId);
                if (cartItem != null)
                {
                    if (quantity <= 0)
                    {
                        _context.ShoppingCart.Remove(cartItem);
                    }
                    else
                    {
                        cartItem.Quantity = quantity;
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item {CartId}", cartId);
                throw;
            }
        }

        public async Task RemoveFromCartAsync(Guid cartId)
        {
            try
            {
                var cartItem = await _context.ShoppingCart.FindAsync(cartId);
                if (cartItem != null)
                {
                    _context.ShoppingCart.Remove(cartItem);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item {CartId}", cartId);
                throw;
            }
        }

        public async Task ClearCartAsync(Guid userId)
        {
            try
            {
                var cartItems = await _context.ShoppingCart
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                _context.ShoppingCart.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleared cart for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
                throw;
            }
        }

        public async Task<int> GetCartItemCountAsync(Guid userId)
        {
            try
            {
                return await _context.ShoppingCart
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => c.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count for user {UserId}", userId);
                return 0;
            }
        }
    }
}