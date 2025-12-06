using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class FeedbackResponse
    {
        public int FeedbackId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class FeedbackDetailResponse : FeedbackResponse
    {
        public int ComUserId { get; set; }
        public string? CompanyName { get; set; }
        public int? CompanyId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserFullName { get; set; }
        public string? Comment { get; set; }
    }
}
