using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("JobCategories")]
    public class JobCategory
    {
        [Key]
        public int JobCategoryId { get; set; }

        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("Category")]
        public int CategoryId { get; set; }

        // Navigation
        public Job Job { get; set; } = null!;
        public Category Category { get; set; } = null!;
    }
}
