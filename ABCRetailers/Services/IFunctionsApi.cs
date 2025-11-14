using ABCRetailers.Models;

namespace ABCRetailers.Services
{
    public interface IFunctionsApi
    {
        Task<List<Customer>> GetCustomersAsync();
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<List<Product>> GetProductsAsync();
        Task<Product> CreateProductAsync(Product product);
        Task<List<Order>> GetOrdersAsync();
        Task<Order> CreateOrderAsync(Order order);
    }
}