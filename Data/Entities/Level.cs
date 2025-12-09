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
    [Table("Levels")]
    public class Level : BaseEntity
    {
        [Key]
        public int LevelId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Navigation
        public ICollection<Job> Jobs { get; set; } = new List<Job>();
    }
}
