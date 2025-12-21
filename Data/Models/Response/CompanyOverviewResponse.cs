namespace Data.Models.Response
{
    public class CompanyOverviewResponse
    {
        public int TotalCompanies { get; set; }
        public int ActiveCompanies { get; set; }
        public int InactiveCompanies { get; set; }
        public int NewCompaniesThisMonth { get; set; }
        public CompanyBySubscriptionStatus SubscriptionBreakdown { get; set; } = new();
        public CompanyByVerificationStatus VerificationBreakdown { get; set; } = new();
    }

    public class CompanyBySubscriptionStatus
    {
        public int WithActiveSubscription { get; set; }
        public int WithExpiredSubscription { get; set; }
        public int WithoutSubscription { get; set; }
    }

    public class CompanyByVerificationStatus
    {
        public int Verified { get; set; }
        public int Pending { get; set; }
        public int Rejected { get; set; }
    }
}
