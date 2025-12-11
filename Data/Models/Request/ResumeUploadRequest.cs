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
}

