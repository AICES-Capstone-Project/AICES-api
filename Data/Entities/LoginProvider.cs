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
    [Table("LoginProviders")]
    public class LoginProvider : BaseEntity
    {
        [Key]
        public int LoginProviderId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public AuthProviderEnum AuthProvider { get; set; } = AuthProviderEnum.Local;

        [MaxLength(255)]
        public string ProviderId { get; set; } = string.Empty; // id từ provider

        // Navigation
        public User User { get; set; } = null!;
    }
}
