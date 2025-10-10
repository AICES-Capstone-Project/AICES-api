using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("Categories")]
    public class Category : BaseEntity
    {
        [Key]
        public int CategoryId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Slug { get; set; }

        // Navigation
        public ICollection<JobPosition>? JobPositions { get; set; }
    }
}
