using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Enum;

namespace Data.Models.Response
{
    public class SelfJobResponse
    {
        public int JobId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Slug { get; set; }
        public string? Requirements { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CategoryName { get; set; }
        public string? SpecializationName { get; set; }
        public List<string>? EmploymentTypes { get; set; }
        public List<CriteriaResponse>? Criteria { get; set; }
        public List<string>? Skills { get; set; }
    }
}


