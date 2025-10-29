using Data.Enum;
using System.Collections.Generic;

namespace Data.Models.Response
{
    public class SelfCompanyResponse
    {
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? TaxCode { get; set; }
        public string? LogoUrl { get; set; }
        public CompanyStatusEnum CompanyStatus { get; set; }
        public string? RejectionReason { get; set; }
        public List<CompanyDocumentResponse> Documents { get; set; } = new List<CompanyDocumentResponse>();
    }

    public class CompanyDocumentResponse
    {
        public string DocumentType { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
    }
}

