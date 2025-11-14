using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models.SQL
{
    public class SqlOrder
    {
        [Key]
        public Guid OrderId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ProductId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalPrice { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Submitted";

        [MaxLength(255)]
        public string? ShippingAddress { get; set; }

        // REMOVE navigation properties to avoid EF confusion
        // public virtual SqlUser? User { get; set; }
    }
}