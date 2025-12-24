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
        Daily,

        [Description("week")]
        Weekly,

        [Description("month")]
        Monthly,

        [Description("year")]
        Yearly,

        [Description("unlimited")]
        Unlimited
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
        /// Calculate end date from start date using proper date arithmetic.
        /// This method handles varying month lengths and leap years correctly.
        /// For Unlimited duration, returns a far future date (100 years).
        /// </summary>
        public static DateTime CalculateEndDate(this DurationEnum duration, DateTime startDate)
        {
            return duration switch
            {
                DurationEnum.Daily => startDate.AddDays(1),
                DurationEnum.Weekly => startDate.AddDays(7),
                DurationEnum.Monthly => startDate.AddMonths(1),  // Handles varying month lengths!
                DurationEnum.Yearly => startDate.AddYears(1),    // Handles leap years!
                DurationEnum.Unlimited => startDate.AddYears(100), // Far future date for free/unlimited plans
                _ => startDate.AddMonths(1)
            };
        }

        /// <summary>
        /// Check if the duration is unlimited (for free/perpetual plans).
        /// </summary>
        public static bool IsUnlimited(this DurationEnum duration)
        {
            return duration == DurationEnum.Unlimited;
        }

        /// <summary>
        /// Chuyển đổi DurationEnum thành số ngày (approximate, for display purposes only).
        /// DO NOT use this for date calculations - use CalculateEndDate() instead.
        /// </summary>
        [Obsolete("Use CalculateEndDate() for accurate date calculations. This method is only for display purposes.")]
        public static int ToDays(this DurationEnum duration)
        {
            return duration switch
            {
                DurationEnum.Daily => 1,
                DurationEnum.Weekly => 7,
                DurationEnum.Monthly => 30,
                DurationEnum.Yearly => 365,
                DurationEnum.Unlimited => 0, // 0 means no expiration
                _ => 30
            };
        }

        /// <summary>
        /// Approximate days for display purposes only (not for calculations).
        /// Returns 0 for Unlimited duration (no expiration).
        /// </summary>
        public static int ApproximateDays(this DurationEnum duration)
        {
            return duration switch
            {
                DurationEnum.Daily => 1,
                DurationEnum.Weekly => 7,
                DurationEnum.Monthly => 30,
                DurationEnum.Yearly => 365,
                DurationEnum.Unlimited => 0, // 0 means no expiration
                _ => 30
            };
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
