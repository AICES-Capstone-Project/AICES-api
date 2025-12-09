namespace Data.Models.Response
{
    public class SystemCompanyStatsResponse
    {
        public int TotalCompanies { get; set; }
        public int ApprovedCompanies { get; set; }
        public int PendingCompanies { get; set; }
        public int RejectedCompanies { get; set; }
        public int SuspendedCompanies { get; set; }
        public int NewCompaniesThisMonth { get; set; }
    }
}

