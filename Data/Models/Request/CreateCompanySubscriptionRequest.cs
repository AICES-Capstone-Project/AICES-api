using Data.Enum;
using System;
using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class CreateCompanySubscriptionRequest
    {
        [Required(ErrorMessage = "Company ID is required")]
        public int CompanyId { get; set; }

        [Required(ErrorMessage = "Subscription ID is required")]
        public int SubscriptionId { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; }

        public bool Renew { get; set; } = false;

        public SubscriptionStatusEnum? Status { get; set; }
    }
}

