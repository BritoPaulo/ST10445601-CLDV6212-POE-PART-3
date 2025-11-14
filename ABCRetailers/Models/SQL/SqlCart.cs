using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models.SQL
{
    public class SqlCart
    {
        [Key]
        public Guid CartId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ProductId { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        // REMOVE navigation properties to avoid EF confusion
        // public virtual SqlUser? User { get; set; }
    }
}