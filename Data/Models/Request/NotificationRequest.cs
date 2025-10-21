using Data.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class NotificationRequest
    {
        [Required(ErrorMessage = "User ID is required")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Notification type is required")]
        public NotificationTypeEnum Type { get; set; }

        [Required(ErrorMessage = "Message is required")]
        public string Message { get; set; } = string.Empty;

        public string? Detail { get; set; }
    }
}
