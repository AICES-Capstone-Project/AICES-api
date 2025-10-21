using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class CompanySubscriptionRequest
    {
        [Required(ErrorMessage = "Company ID is required")]
        public int CompanyId { get; set; }

        [Required(ErrorMessage = "Subscription ID is required")]
        public int SubscriptionId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
