using Data.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Data.Entities
{
    [Table("Users")]
    public class User : BaseEntity
    {
        [Key]
        public int UserId { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? AuthProvider { get; set; } // google, local, facebook...

        public string? ProviderId { get; set; } // id từ provider

        // Navigation
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public Profile? Profile { get; set; }
    }
}
