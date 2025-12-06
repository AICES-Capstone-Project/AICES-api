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
    [Table("Feedbacks")]
    public class Feedback : BaseEntity
    {
        [Key]
        public int FeedbackId { get; set; }

        [ForeignKey("CompanyUser")]
        public int ComUserId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        [MaxLength(5000)]
        public string? Comment { get; set; }

        // Navigation
        public CompanyUser CompanyUser { get; set; } = null!;
    }
}
