using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ABCRetailers.Controllers
{
    [Authorize] // Allow both customers and admins to access
    public class ProductController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IAzureStorageService storageService, ILogger<ProductController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        // Allow both customers and admins to view products
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _storageService.GetAllEntitiesAsync<Product>();
                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                TempData["Error"] = "Error loading products.";
                return View(new List<Product>());
            }
        }

        // Only admins can create products
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            _logger.LogInformation("Create product called with Price: {Price}", product.Price);

            if (ModelState.IsValid)
            {
                try
                {
                    // Validate price
                    if (product.Price <= 0)
                    {
                        ModelState.AddModelError("Price", "Price must be greater than $0.00");
                        return View(product);
                    }

                    // Set PartitionKey and RowKey
                    product.PartitionKey = "Product";
                    product.RowKey = Guid.NewGuid().ToString();

                    // Upload image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        try
                        {
                            _logger.LogInformation("Attempting to upload image: {FileName} ({Size} bytes)",
                                imageFile.FileName, imageFile.Length);

                            var imageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                            product.ImageUrl = imageUrl;
                            _logger.LogInformation("✅ Image uploaded successfully: {ImageUrl}", imageUrl);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Failed to upload image: {FileName}", imageFile.FileName);
                            ModelState.AddModelError("", $"Failed to upload image: {ex.Message}");
                            return View(product);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("No image file provided for product");
                        product.ImageUrl = string.Empty;
                    }

                    // Add the product to storage
                    await _storageService.AddEntityAsync(product);
                    _logger.LogInformation("✅ Product saved successfully: {ProductName} with price {Price}",
                        product.ProductName, product.Price);

                    TempData["Success"] = $"Product '{product.ProductName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error creating product");
                    ModelState.AddModelError("", $"Error creating product: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning("Model state is invalid: {Errors}",
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            }

            return View(product);
        }

        // Only admins can edit products
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var product = await _storageService.GetEntityAsync<Product>("Product", id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var originalProduct = await _storageService.GetEntityAsync<Product>("Product", product.RowKey);
                    if (originalProduct == null)
                    {
                        return NotFound();
                    }

                    originalProduct.ProductName = product.ProductName;
                    originalProduct.Description = product.Description;
                    originalProduct.Price = product.Price;
                    originalProduct.StockAvailable = product.StockAvailable;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        try
                        {
                            var imageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                            originalProduct.ImageUrl = imageUrl;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to upload image during edit");
                            ModelState.AddModelError("", $"Failed to upload image: {ex.Message}");
                            return View(product);
                        }
                    }

                    await _storageService.UpdateEntityAsync(originalProduct);
                    TempData["Success"] = "Product updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product: {Message}", ex.Message);
                    ModelState.AddModelError("", $"Error updating product: {ex.Message}");
                }
            }

            return View(product);
        }

        // Only admins can delete products
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storageService.DeleteEntityAsync<Product>("Product", id);
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Allow both customers and admins to view product details
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var product = await _storageService.GetEntityAsync<Product>("Product", id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
    }
}