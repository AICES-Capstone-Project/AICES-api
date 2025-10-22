using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class CompanyResponse
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? CompanyStatus { get; set; }
        public int CreatedBy { get; set; }
        public int? ApprovalBy { get; set; }
        public string? RejectionReason { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
