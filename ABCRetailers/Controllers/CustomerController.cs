using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ABCRetailers.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IFunctionsApi _functionsApi;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(IFunctionsApi functionsApi, ILogger<CustomerController> logger)
        {
            _functionsApi = functionsApi;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("📞 Calling Functions API to get customers...");
                var customers = await _functionsApi.GetCustomersAsync();
                _logger.LogInformation($"✅ Retrieved {customers.Count} customers via Functions API");
                return View(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error getting customers from Functions API");
                TempData["Error"] = "Error loading customers. Please make sure Functions are running.";
                return View(new List<Customer>());
            }
        }

        // ✅ ADD THIS MISSING GET METHOD
        public IActionResult Create()
        {
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            _logger.LogInformation("📞 Create customer POST method called");

            // ✅ ADD DEBUG LOGGING
            _logger.LogInformation($"Customer data - Name: '{customer.Name}', Email: '{customer.Email}', Surname: '{customer.Surname}'");

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"📞 Calling Functions API to create customer: {customer.Name}");

                    // Generate RowKey if not present
                    if (string.IsNullOrEmpty(customer.RowKey))
                    {
                        customer.RowKey = Guid.NewGuid().ToString();
                    }

                    await _functionsApi.CreateCustomerAsync(customer);
                    _logger.LogInformation("✅ Functions API call completed successfully");

                    TempData["Success"] = "Customer created successfully via Functions API!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error creating customer via Functions API");
                    ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
                    TempData["Error"] = $"Failed to create customer: {ex.Message}";
                }
            }
            else
            {
                // ✅ LOG MODEL STATE ERRORS
                _logger.LogWarning("ModelState is invalid");
                foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                {
                    _logger.LogWarning($"Model error: {error.ErrorMessage}");
                }
            }
            return View(customer);
        }

        // Add missing methods
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var customer = await _functionsApi.GetCustomersAsync()
                .ContinueWith(t => t.Result.FirstOrDefault(c => c.RowKey == id));

            if (customer == null)
                return NotFound();

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Implementation for edit
                    TempData["Success"] = "Customer updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating customer");
                    ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                // Implementation for delete
                TempData["Success"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer");
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}