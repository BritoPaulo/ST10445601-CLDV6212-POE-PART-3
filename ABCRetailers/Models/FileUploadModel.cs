// Fix the typo in the property name
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ABCRetailers.Models
{
    public class FileUploadModel
    {
        [Required]
        [Display(Name = "Proof of Payment")]
        public IFormFile ProofOfPayment { get; set; }  // Fixed the typo from "ProodOfPayment"

        [Display(Name = "Order ID")]
        public string? OrderId { get; set; }

        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }
    }
}