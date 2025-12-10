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

        // Specialization is required (replaces categories)
        [Required(ErrorMessage = "Specialization ID is required")]
        [DefaultValue(1)]
        public int? SpecializationId { get; set; }

        // Employment Type IDs are required and must contain at least one ID
        [Required(ErrorMessage = "At least one Employment Type ID is required")]
        [MinLength(1, ErrorMessage = "At least one employment type ID must be provided.")]
        [DefaultValue(new int[] { 1 })]
        public List<int>? EmploymentTypeIds { get; set; }

        // Optional: attach required skills to job
        [DefaultValue(new int[] { })]
        public List<int>? SkillIds { get; set; }

        // Optional: Level ID for the job
        public int? LevelId { get; set; }

        // Optional: Language IDs for the job (many-to-many)
        [DefaultValue(new int[] { })]
        public List<int>? LanguageIds { get; set; }

        // Criteria are required and must contain between 2 and 19 items
        [Required(ErrorMessage = "Criteria are required")]
        [MinLength(2, ErrorMessage = "At least 2 criteria must be provided.")]
        [MaxLength(19, ErrorMessage = "Maximum of 19 criteria can be provided.")]
        public List<CriteriaRequest>? Criteria { get; set; }
    }
}


