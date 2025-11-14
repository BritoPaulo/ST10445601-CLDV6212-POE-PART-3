namespace ABCRetailers.Models
{
    public class Cart
    {
        public Guid CartId { get; set; }
        public Guid UserId { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public DateTime AddedDate { get; set; }

        // Navigation properties
        public Product? Product { get; set; }
        public LegacyUser? User { get; set; }
    }
}