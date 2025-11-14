using System.Text;
using System.Text.Json;
using ABCRetailers.Models;

namespace ABCRetailers.Services
{
    public class FunctionsApiClient : IFunctionsApi
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FunctionsApiClient> _logger;

        public FunctionsApiClient(HttpClient httpClient, ILogger<FunctionsApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // Customer methods
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                _logger.LogInformation("Calling Functions API: GET /api/customers");
                var response = await _httpClient.GetAsync("customers");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get customers: {response.StatusCode}");
                    return new List<Customer>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Functions API response: {content}");

                return DeserializeListResponse<Customer>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers from Functions API");
                return new List<Customer>();
            }
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            try
            {
                _logger.LogInformation("Calling Functions API: POST /api/customers");

                // ✅ FIX: Create the exact structure that Functions expect
                var customerData = new
                {
                    name = customer.Name ?? "",
                    surname = customer.Surname ?? "",
                    username = customer.Username ?? "",
                    email = customer.Email ?? "",
                    shippingAddress = customer.ShippingAddress ?? ""
                };

                var json = JsonSerializer.Serialize(customerData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation($"Sending customer data: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("customers", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Functions API returned error: {response.StatusCode} - {errorContent}");
                    throw new HttpRequestException($"Functions API error: {response.StatusCode} - {errorContent}");
                }

                _logger.LogInformation("Customer created successfully via Functions API");
                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer via Functions API");
                throw;
            }
        }

        // Product methods
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                _logger.LogInformation("Calling Functions API: GET /api/products");
                var response = await _httpClient.GetAsync("products");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get products: {response.StatusCode}");
                    return new List<Product>();
                }

                var content = await response.Content.ReadAsStringAsync();
                return DeserializeListResponse<Product>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from Functions API");
                return new List<Product>();
            }
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            try
            {
                _logger.LogInformation("Calling Functions API: POST /api/products");

                var productData = new
                {
                    productName = product.ProductName,
                    description = product.Description,
                    price = product.Price,
                    stockAvailable = product.StockAvailable,
                    imageUrl = product.ImageUrl
                };

                var json = JsonSerializer.Serialize(productData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("products", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Functions API error: {response.StatusCode} - {errorContent}");
                }

                _logger.LogInformation("Product created successfully via Functions API");
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product via Functions API");
                throw;
            }
        }

        // Order methods
        public async Task<List<Order>> GetOrdersAsync()
        {
            try
            {
                _logger.LogInformation("Calling Functions API: GET /api/orders");
                var response = await _httpClient.GetAsync("orders");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to get orders: {response.StatusCode}");
                    return new List<Order>();
                }

                var content = await response.Content.ReadAsStringAsync();
                return DeserializeListResponse<Order>(content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders from Functions API");
                return new List<Order>();
            }
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            try
            {
                _logger.LogInformation("Calling Functions API: POST /api/orders");

                var orderData = new
                {
                    customerId = order.CustomerId,
                    customerName = order.Username,
                    productId = order.ProductId,
                    productName = order.ProductName,
                    orderDate = order.OrderDate,
                    quantity = order.Quantity,
                    unitPrice = order.UnitPrice,
                    totalPrice = order.TotalPrice,
                    status = order.Status
                };

                var json = JsonSerializer.Serialize(orderData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("orders", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Functions API error: {response.StatusCode} - {errorContent}");
                }

                _logger.LogInformation("Order created successfully via Functions API");
                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order via Functions API");
                throw;
            }
        }

        private List<T> DeserializeListResponse<T>(string jsonContent)
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            try
            {
                // Try to deserialize as Functions API response
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                // Check if it has an "items" property
                if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    var itemsJson = itemsElement.GetRawText();
                    return JsonSerializer.Deserialize<List<T>>(itemsJson, options) ?? new List<T>();
                }

                // Check if it has a "data" property  
                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    var dataJson = dataElement.GetRawText();
                    return JsonSerializer.Deserialize<List<T>>(dataJson, options) ?? new List<T>();
                }

                // Fallback: try to deserialize as simple array
                return JsonSerializer.Deserialize<List<T>>(jsonContent, options) ?? new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse API response");
                return new List<T>();
            }
        }
    }

    // Helper class for API responses
    public class FunctionsApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<T> Items { get; set; } = new List<T>();
        public T Data { get; set; }
    }
}