using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("JobLanguages")]
    public class JobLanguage
    {
        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Language")]
        public int LanguageId { get; set; }

        // Navigation
        public Job Job { get; set; } = null!;
        public Language Language { get; set; } = null!;
    }
}
