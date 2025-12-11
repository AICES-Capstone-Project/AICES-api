using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum ResumeStatusEnum
    {
        // Pending: When a resume is uploaded and waiting for processing
        Pending,
        // Completed: When a resume is processed successfully
        Completed,
        // Failed: When a resume is processed but failed
        Failed,
        // Timeout: When a resume is processed but timed out
        Timeout,
        // InvalidJobData: When a resume is processed but the job data is invalid
        InvalidJobData,
        // InvalidResumeData: When a resume is processed but the resume data is invalid
        InvalidResumeData,
        // JobTitleNotMatched: When a resume is processed but the job title is not matched
        JobTitleNotMatched,
        // Canceled: When a resume is canceled
        Canceled,
        // CorruptedFile: When a resume file is corrupted, File bị hỏng, AI/Parser không đọc được nội dung.
        CorruptedFile,
        // DuplicateResume: When a resume is processed but the resume is already exists
        DuplicateResume,
        // ServerError: When a resume is processed but the server error
        ServerError,   
    }
}
