using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("JobSkills")]
    public class JobSkill
    {
        [Key]
        public int JobSkillId { get; set; }

        [ForeignKey("Skill")]
        public int SkillId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        // Navigation
        public Skill Skill { get; set; } = null!;
        public Job Job { get; set; } = null!;
    }
}
