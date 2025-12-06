using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum DurationEnum
    {
        [Description("day")]
        Daily = 1,      // 1 day

        [Description("week")]
        Weekly = 7,     // 7 days

        [Description("month")]
        Monthly = 30,   // 30 days

        [Description("year")]
        Yearly = 365    // 365 days
    }

    public static class DurationEnumExtensions
    {
        /// <summary>
        /// Lấy string value của DurationEnum (đồng bộ với Stripe: "day", "week", "month", "year")
        /// </summary>
        public static string ToStripeString(this DurationEnum duration)
        {
            FieldInfo? field = duration.GetType().GetField(duration.ToString());
            if (field != null)
            {
                DescriptionAttribute? attribute = field.GetCustomAttribute<DescriptionAttribute>();
                return attribute?.Description ?? duration.ToString().ToLower();
            }
            return duration.ToString().ToLower();
        }

        /// <summary>
        /// Chuyển đổi DurationEnum thành số ngày
        /// </summary>
        public static int ToDays(this DurationEnum duration)
        {
            return (int)duration;
        }

        /// <summary>
        /// Chuyển đổi string từ Stripe sang DurationEnum
        /// </summary>
        public static DurationEnum FromStripeString(string stripeValue)
        {
            foreach (DurationEnum duration in System.Enum.GetValues(typeof(DurationEnum)))
            {
                if (duration.ToStripeString().Equals(stripeValue, StringComparison.OrdinalIgnoreCase))
                {
                    return duration;
                }
            }
            throw new ArgumentException($"Invalid Stripe duration value: {stripeValue}");
        }
    }
}
