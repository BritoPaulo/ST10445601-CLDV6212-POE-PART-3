using System.Security.Claims;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailers.Controllers
{
    [Authorize] // Both customers and admins can access
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, IAzureStorageService storageService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _storageService = storageService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out Guid userId))
            {
                return userId;
            }
            throw new InvalidOperationException("User ID not found or invalid");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _cartService.GetCartItemsAsync(userId);
                return View(cartItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shopping cart");
                TempData["Error"] = "Error loading shopping cart.";
                return View(new List<CartItemViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(string productId, int quantity = 1)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Check if product exists and has sufficient stock
                var product = await _storageService.GetEntityAsync<Product>("Product", productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found." });
                }

                if (product.StockAvailable < quantity)
                {
                    return Json(new { success = false, message = $"Insufficient stock. Only {product.StockAvailable} available." });
                }

                await _cartService.AddToCartAsync(userId, productId, quantity);

                var cartCount = await _cartService.GetCartItemCountAsync(userId);

                return Json(new
                {
                    success = true,
                    message = "Product added to cart!",
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding product to cart: {ProductId}", productId);
                return Json(new { success = false, message = "Error adding product to cart." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(Guid cartId, int quantity)
        {
            try
            {
                if (quantity <= 0)
                {
                    await _cartService.RemoveFromCartAsync(cartId);
                }
                else
                {
                    await _cartService.UpdateCartItemAsync(cartId, quantity);
                }

                var userId = GetCurrentUserId();
                var cartItems = await _cartService.GetCartItemsAsync(userId);
                var cartCount = await _cartService.GetCartItemCountAsync(userId);
                var totalAmount = cartItems.Sum(item => item.TotalPrice);

                return Json(new
                {
                    success = true,
                    cartCount = cartCount,
                    totalAmount = totalAmount.ToString("C")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart quantity for cart ID: {CartId}", cartId);
                return Json(new { success = false, message = "Error updating quantity." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveItem(Guid cartId)
        {
            try
            {
                await _cartService.RemoveFromCartAsync(cartId);

                var userId = GetCurrentUserId();
                var cartCount = await _cartService.GetCartItemCountAsync(userId);

                return Json(new
                {
                    success = true,
                    message = "Item removed from cart.",
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart: {CartId}", cartId);
                return Json(new { success = false, message = "Error removing item from cart." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _cartService.ClearCartAsync(userId);

                TempData["Success"] = "Shopping cart cleared successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing shopping cart");
                TempData["Error"] = "Error clearing shopping cart.";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                var count = await _cartService.GetCartItemCountAsync(userId);
                return Json(new { count = count });
            }
            catch
            {
                return Json(new { count = 0 });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _cartService.GetCartItemsAsync(userId);

                if (!cartItems.Any())
                {
                    TempData["Error"] = "Your cart is empty.";
                    return RedirectToAction("Index");
                }

                // Check stock availability
                foreach (var item in cartItems)
                {
                    var product = await _storageService.GetEntityAsync<Product>("Product", item.ProductId);
                    if (product == null || product.StockAvailable < item.Quantity)
                    {
                        TempData["Error"] = $"Insufficient stock for {item.ProductName}. Please update your cart.";
                        return RedirectToAction("Index");
                    }
                }

                return View(cartItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during checkout");
                TempData["Error"] = "Error during checkout process.";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _cartService.GetCartItemsAsync(userId);

                if (!cartItems.Any())
                {
                    TempData["Error"] = "Your cart is empty.";
                    return RedirectToAction("Index");
                }

                // Create orders for each item
                foreach (var item in cartItems)
                {
                    var product = await _storageService.GetEntityAsync<Product>("Product", item.ProductId);
                    if (product != null)
                    {
                        var order = new Order
                        {
                            CustomerId = userId.ToString(),
                            Username = User.Identity!.Name!,
                            ProductId = item.ProductId,
                            ProductName = item.ProductName,
                            OrderDate = DateTime.UtcNow,
                            Quantity = item.Quantity,
                            UnitPrice = item.Price,
                            TotalPrice = item.TotalPrice,
                            Status = "Submitted",
                            SqlUserId = userId
                        };

                        await _storageService.AddEntityAsync(order);

                        // Update product stock
                        product.StockAvailable -= item.Quantity;
                        await _storageService.UpdateEntityAsync(product);
                    }
                }

                // Clear the cart
                await _cartService.ClearCartAsync(userId);

                TempData["Success"] = "Order placed successfully! Thank you for your purchase.";
                return RedirectToAction("Index", "Home"); // Redirect to home instead of Order index
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                TempData["Error"] = "Error placing order. Please try again.";
                return RedirectToAction("Checkout");
            }
        }

        // Allow admins to view customer orders (optional)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CustomerOrders()
        {
            try
            {
                var orders = await _storageService.GetAllEntitiesAsync<Order>();
                return View(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading customer orders");
                TempData["Error"] = "Error loading customer orders.";
                return View(new List<Order>());
            }
        }
    }
}