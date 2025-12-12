using System.ComponentModel.DataAnnotations;

namespace Data.Models.Request
{
    public class UpdateAdjustedScoreRequest
    {
        [Required(ErrorMessage = "Adjusted score is required")]
        [Range(0, 100, ErrorMessage = "Adjusted score must be between 0 and 100")]
        public decimal AdjustedScore { get; set; }
    }
}
