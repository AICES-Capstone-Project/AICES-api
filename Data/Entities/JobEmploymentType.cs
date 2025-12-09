using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("JobEmploymentTypes")]
    public class JobEmploymentType
    {
        [ForeignKey("Job")]
        public int JobId { get; set; }

        [ForeignKey("EmploymentType")]
        public int EmployTypeId { get; set; }

        // Navigation
        public Job Job { get; set; } = null!;
        public EmploymentType EmploymentType { get; set; } = null!;
    }
}
