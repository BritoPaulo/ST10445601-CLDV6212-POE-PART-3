namespace ABCRetailers.Models.ViewModels
{
    public class CartItemViewModel
    {
        public Guid CartId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public double TotalPrice => Price * Quantity;
        public int StockAvailable { get; set; }
    }
}