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
    [Table("Languages")]
    public class Language : BaseEntity
    {
        [Key]
        public int LanguageId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Navigation
        public ICollection<JobLanguage> JobLanguages { get; set; } = new List<JobLanguage>();
    }
}
