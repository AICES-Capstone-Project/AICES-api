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
    [Table("RefreshTokens")]
    public class RefreshToken : BaseEntity
    {
        [Key]
        public int RefreshTokenId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiryDate { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
