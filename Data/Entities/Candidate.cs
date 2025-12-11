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
    [Table("Candidates")]
    public class Candidate : BaseEntity
    {
        [Key]
        public int CandidateId { get; set; }

        [Required, MaxLength(255)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }
        public string? MatchSkills { get; set; }
        public string? MissingSkills { get; set; }

        // Navigation
        public ICollection<Resume> Resumes { get; set; } = new List<Resume>();
    }
}
