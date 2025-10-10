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
    [Table("JobPositions")]
    public class JobPosition : BaseEntity
    {
        [Key]
        public int PositionId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [MaxLength(255)]
        public string? Slug { get; set; }

        public string? Requirements { get; set; }

        [MaxLength(50)]
        public string? WorkType { get; set; }

        [MaxLength(100)]
        public string? Duration { get; set; }

        [MaxLength(255)]
        public string? JobLevels { get; set; }

        [MaxLength(255)]
        public string? SalaryRange { get; set; }

        public int NumberPositions { get; set; } = 1;

        // Navigation
        public Job Job { get; set; } = null!;
        public Category Category { get; set; } = null!;
        public ICollection<Favorite>? Favorites { get; set; }
    }
}
