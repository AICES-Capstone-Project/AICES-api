using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum NotificationTypeEnum
    {
        Resume,
        Job,
        Payment,
        Subscription,
        Company,
        System,
        // // ===== COMPANY RECRUITER =====
        // HR_Recruiter_CompanyRejected,
        // HR_Recruiter_ResumeUploaded,
        // HR_Recruiter_ResumeParsed,
        // HR_Recruiter_ScoringCompleted,
        // HR_Recruiter_CandidateShortlisted,
        // HR_Recruiter_JobClosed,
        // HR_Recruiter_SubscriptionLow,
        // HR_Recruiter_SubscriptionExpired,
        // HR_Recruiter_PaymentSuccess,
        // HR_Recruiter_PaymentFailed,

        // // ===== COMPANY MANAGER =====
        // HR_Manager_CompanyApproved,
        // HR_Manager_CompanySuspended,
        // HR_Manager_NewRecruiterAdded,
        // HR_Manager_RecruiterRemoved,
        // HR_Manager_SubscriptionChanged,
        // HR_Manager_PaymentSuccess,
        // HR_Manager_PaymentFailed,
        // HR_Manager_UsageLimitReached,
        // HR_Manager_AIConfigUpdated,

        // // ===== SYSTEM STAFF =====
        // System_Staff_NewSupportTicket,
        // System_Staff_TicketAssigned,
        // System_Staff_ContentApproved,
        // System_Staff_ContentRejected,

        // // ===== SYSTEM MANAGER =====
        // System_Manager_NewStaffCreated,
        // System_Manager_StaffDisabled,
        // System_Manager_ModelRetrained,
        // System_Manager_ModelPerformanceAlert,
        // System_Manager_NewSubscriptionPackage,
        // System_Manager_PaymentReviewNeeded,

        // // ===== SYSTEM ADMIN =====
        // System_Admin_NewCompanyRegistration,
        // System_Admin_CompanyApproved,
        // System_Admin_CompanyRejected,
        // System_Admin_NewSystemRequest,
        // System_Admin_PaymentDispute,
        // System_Admin_CriticalSystemError,
    }
}

