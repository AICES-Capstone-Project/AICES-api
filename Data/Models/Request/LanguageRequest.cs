using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class LanguageRequest
    {
        [Required(ErrorMessage = "Language name is required.")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}

