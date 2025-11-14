using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ABCRetailers.Controllers
{
    public class HomeController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly IFunctionsApi _functionsApi;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IAzureStorageService storageService, IFunctionsApi functionsApi, ILogger<HomeController> logger)
        {
            _storageService = storageService;
            _functionsApi = functionsApi;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("Loading dashboard data from Functions API...");

                // Try to use Functions API first
                var products = await _functionsApi.GetProductsAsync();
                var customers = await _functionsApi.GetCustomersAsync();

                // Orders might not be implemented in Functions API yet, so use storage
                var orders = await _storageService.GetAllEntitiesAsync<Order>();

                _logger.LogInformation($"Retrieved {products.Count} products, {customers.Count} customers, {orders.Count} orders");

                var viewModel = new HomeViewModel
                {
                    FeaturedProducts = products.Take(5).ToList(),
                    ProductCount = products.Count,
                    CustomerCount = customers.Count,
                    OrderCount = orders.Count
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Functions API failed, falling back to direct storage");

                // Fallback to storage if Functions API fails
                try
                {
                    var products = await _storageService.GetAllEntitiesAsync<Product>();
                    var customers = await _storageService.GetAllEntitiesAsync<Customer>();
                    var orders = await _storageService.GetAllEntitiesAsync<Order>();

                    var viewModel = new HomeViewModel
                    {
                        FeaturedProducts = products.Take(5).ToList(),
                        ProductCount = products.Count,
                        CustomerCount = customers.Count,
                        OrderCount = orders.Count
                    };

                    TempData["Warning"] = "Using direct storage (Functions API unavailable)";
                    return View(viewModel);
                }
                catch (Exception storageEx)
                {
                    _logger.LogError(storageEx, "Both Functions API and storage failed");

                    // Final fallback - empty data
                    var viewModel = new HomeViewModel
                    {
                        FeaturedProducts = new List<Product>(),
                        ProductCount = 0,
                        CustomerCount = 0,
                        OrderCount = 0
                    };

                    TempData["Error"] = "Unable to load dashboard data. Please check your connection.";
                    return View(viewModel);
                }
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // ADD THIS ACTION METHOD FOR CONTACT US
        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> InitializeStorage()
        {
            try
            {
                await _storageService.GetAllEntitiesAsync<Customer>();
                TempData["Success"] = "Azure Storage initialized successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to initialize storage: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> TestFunctionsApi()
        {
            try
            {
                var customers = await _functionsApi.GetCustomersAsync();
                var products = await _functionsApi.GetProductsAsync();

                TempData["Success"] = $"Functions API working! Retrieved {customers.Count} customers and {products.Count} products";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Functions API test failed: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}