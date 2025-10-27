using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("Skills")]
    public class Skill : BaseEntity
    {
        [Key]
        public int SkillId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Navigation
        public ICollection<JobSkill>? JobSkills { get; set; }
    }
}
