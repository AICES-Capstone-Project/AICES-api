using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class JobWithTargetRequest
    {
        [Required(ErrorMessage = "Job ID is required.")]
        public int JobId { get; set; }

        [Required(ErrorMessage = "Target quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Target quantity must be at least 1.")]
        [DefaultValue(1)]
        public int TargetQuantity { get; set; }
    }
}

