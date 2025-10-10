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
    [Table("Favorites")]
    public class Favorite : BaseEntity
    {
        [Key]
        public int FavoriteId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [ForeignKey("JobPosition")]
        public int PositionId { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public JobPosition JobPosition { get; set; } = null!;
    }
}
