namespace Data.Models.Response
{
    public class SystemCompanySubStatsResponse
    {
        public int TotalCompanySubscriptions { get; set; }
        public int Active { get; set; }
        public int Expired { get; set; }
        public int NewThisMonth { get; set; }
    }
}

