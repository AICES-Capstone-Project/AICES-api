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
        public string? Address { get; set; }
        public string? LogoUrl { get; set; }
        public string? CompanyStatus { get; set; }
        public string? CreatedBy { get; set; }
        public string? ApprovalBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CompanyDetailResponse
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? TaxCode { get; set; }
        public string? LogoUrl { get; set; }
        public string? CompanyStatus { get; set; }
        public string? CreatedBy { get; set; }
        public string? ApprovalBy { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<CompanyDocumentResponse> Documents { get; set; } = new List<CompanyDocumentResponse>();
    }
}
