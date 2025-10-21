using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class SubscriptionRequest
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be a positive value")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Duration in days is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Duration must be at least 1 day")]
        public int DurationDays { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }
    }
}
