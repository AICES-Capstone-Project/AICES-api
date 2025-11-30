using Data.Entities.Base;
using Data.Enum;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("Invitations")]
    public class Invitation : BaseEntity
    {
        [Key]
        public int InvitationId { get; set; }

        [ForeignKey("Sender")]
        public int SenderId { get; set; }

        [ForeignKey("Receiver")]
        public int ReceiverId { get; set; }

        [Required, MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        public InvitationStatusEnum InvitationStatus { get; set; }

        // Navigation
        public User Sender { get; set; } = null!;
        public User Receiver { get; set; } = null!;
    }
}
