using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Settings
{
    namespace Data.Settings
    {
        public class StripeSettings
        {
            public string SecretKey { get; set; } = string.Empty;
            public string PublishableKey { get; set; } = string.Empty;
            public string WebhookSecret { get; set; } = string.Empty;
            public decimal VndToUsdRate { get; set; } = 0.000041M;
        }
    }


}
