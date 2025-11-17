using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum NotificationTypeEnum
    {
        // --- System & Admin ---
        System = 1,
        CompanyApproved = 2,
        Job = 3,
        Payment = 4,
        Subscription = 5,
        User = 6,
        CompanyRejected = 7,
        CompanySuspended = 8,


        // --- Job related ---
        JobCreated = 10,               // ? HR ??ng job m?i
        JobRejected = 11,              // Job b? t? ch?i duy?t
        JobPublished = 12,             // Job ???c duy?t thành công
        JobArchived = 13,              // Job b? ?n / l?u tr?

        // --- Application related ---
        CandidateApplied = 20,         // ? CV v?a ???c apply
        CandidateWithdraw = 21,        // ?ng viên rút h? s?

        // --- Company / Subscription related ---
        CompanySubscriptionPurchased = 30, // ? Company ??ng ký gói m?i
        SubscriptionExpired = 31,

        // --- Payment related ---
        PaymentSuccess = 40,
        PaymentFailed = 41,

        // --- User related ---
        UserRegistered = 50,
        UserDeactivated = 51
    }
}

