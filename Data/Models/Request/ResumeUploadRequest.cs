using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Data.Models.Request
{
    public class ResumeUploadRequest
    {
        [Required(ErrorMessage = "Campaign ID is required.")]
        public int CampaignId { get; set; }

        [Required(ErrorMessage = "Job ID is required.")]
        public int JobId { get; set; }

        [Required(ErrorMessage = "Resume file is required.")]
        public IFormFile? File { get; set; }
    }

    public class ResumeBatchUploadRequest
    {
        [Required(ErrorMessage = "Campaign ID is required.")]
        public int CampaignId { get; set; }

        [Required(ErrorMessage = "Job ID is required.")]
        public int JobId { get; set; }

        [Required(ErrorMessage = "At least one resume file is required.")]
        [MaxLength(100, ErrorMessage = "Maximum 100 files can be uploaded at once.")]
        public IFormFileCollection? Files { get; set; }
    }
}

