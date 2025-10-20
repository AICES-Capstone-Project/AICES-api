using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Request
{
    public class JobRequest
    {
        [Required(ErrorMessage = "Title is required")]
        [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
        [DefaultValue("Software Developer")]
        public string? Title { get; set; }

        [DefaultValue("We are looking for a skilled developer to join our team.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Requirements is required")]
        [MinLength(10, ErrorMessage = "Requirements must be at least 10 characters.")]
        [DefaultValue("3+ years experience with C#, .NET, and SQL.")]
        public string? Requirements { get; set; }

        // Category IDs are required and must contain at least one ID
        [Required(ErrorMessage = "At least one Category ID is required")]
        [MinLength(1, ErrorMessage = "At least one category ID must be provided.")]
        [DefaultValue(new int[] { 1 })]
        public List<int>? CategoryIds { get; set; }

        // Employment Type IDs are required and must contain at least one ID
        [Required(ErrorMessage = "At least one Employment Type ID is required")]
        [MinLength(1, ErrorMessage = "At least one employment type ID must be provided.")]
        [DefaultValue(new int[] { 1 })]
        public List<int>? EmploymentTypeIds { get; set; }
    }
}


