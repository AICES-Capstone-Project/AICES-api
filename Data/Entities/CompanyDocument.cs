using Data.Entities.Base;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("CompanyDocuments")]
    public class CompanyDocument : BaseEntity
    {
        [Key]
        public int DocId { get; set; }

        [ForeignKey("Company")]
        public int CompanyId { get; set; }

        [MaxLength(100)]
        public string? DocumentType { get; set; }

        public string? FileUrl { get; set; }

        // Navigation
        public Company Company { get; set; } = null!;
    }
}
