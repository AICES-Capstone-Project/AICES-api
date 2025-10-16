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
    [Table("BannerConfigs")]
    public class BannerConfig : BaseEntity
    {
        [Key]
        public int BannerId { get; set; }

        [Required, MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        public string? ColorCode { get; set; }

        public string? Source { get; set; }
    }
}
