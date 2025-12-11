using System;

namespace Data.Models.Response
{
    public class ResumeEffectivenessResponse
    {
        public ProcessingMetrics Processing { get; set; } = new ProcessingMetrics();
    }

    public class ProcessingMetrics
    {
        /// <summary>Total số resume (IsActive = true).</summary>
        public int TotalResumes { get; set; }

        /// <summary>Số resume đã xử lý thành công (Status = Completed).</summary>
        public int ProcessedResumes { get; set; }

        /// <summary>Tỷ lệ thành công (%) = Completed / Total.</summary>
        public decimal ProcessingSuccessRate { get; set; }

        /// <summary>Số resume thất bại (Failed, Timeout, CorruptedFile, InvalidResumeData, ServerError).</summary>
        public int FailedResumes { get; set; }

        /// <summary>Số resume đang chờ xử lý (Pending).</summary>
        public int PendingResumes { get; set; }
    }
}

