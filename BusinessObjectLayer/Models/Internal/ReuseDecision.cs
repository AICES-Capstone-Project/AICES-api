using Data.Entities;

namespace BusinessObjectLayer.Models.Internal
{
    /// <summary>
    /// Internal DTO for determining resume reuse strategy.
    /// Used by ResumeService to decide whether to clone, reuse, or create new resume.
    /// </summary>
    public class ReuseDecision
    {
        /// <summary>
        /// True if the resume result should be cloned from an existing application
        /// (same resume + same job, no quota consumed)
        /// </summary>
        public bool ShouldClone { get; set; }

        /// <summary>
        /// True if the resume data should be reused for a new job application
        /// (same resume + different job, quota consumed for new scoring)
        /// </summary>
        public bool ShouldReuse { get; set; }

        /// <summary>
        /// The existing application to clone results from (if ShouldClone = true)
        /// </summary>
        public ResumeApplication? ExistingApplication { get; set; }
    }
}

