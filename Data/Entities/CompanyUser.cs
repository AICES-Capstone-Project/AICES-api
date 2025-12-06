using Data.Entities.Base;
using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Entities
{
    [Table("CompanyUsers")]
    public class CompanyUser : BaseEntity
    {
        [Key]
        public int ComUserId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("Company")]
        public int? CompanyId { get; set; } 

        public JoinStatusEnum JoinStatus { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Company? Company { get; set; } 
        public ICollection<Job>? Jobs { get; set; }
        public ICollection<Feedback>? Feedbacks { get; set; }
    }
}


