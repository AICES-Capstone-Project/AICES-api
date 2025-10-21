using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class PaymentRequest
    {
        [Required(ErrorMessage = "Company ID is required")]
        public int CompanyId { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Amount must be a positive value")]
        public decimal Amount { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        public bool Status { get; set; } = false;

        [MaxLength(255)]
        public string? TransactionId { get; set; }
    }
}
