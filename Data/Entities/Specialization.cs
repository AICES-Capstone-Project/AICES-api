using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("Specializations")]
    public class Specialization : BaseEntity
    {
        [Key]
        public int SpecializationId { get; set; }

        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Navigation
        public Category Category { get; set; } = null!;
        public ICollection<Job>? Jobs { get; set; }
    }
}


