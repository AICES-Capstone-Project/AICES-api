using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Settings
{
    public class PaymentSettings
    {
        public decimal VndToUsdRate { get; set; } = 25000;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public string DefaultInterval { get; set; } = "monthly";
    }
}
