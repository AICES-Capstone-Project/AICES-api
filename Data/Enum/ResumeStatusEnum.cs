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
        // Completed: When a resume is processed successfully (at least once)
        Completed,
        // Failed: When a resume is processed but failed due to file/technical issues
        Failed,
        // Timeout: When a resume processing timed out
        Timeout,
        // CorruptedFile: When a resume file is corrupted, File bị hỏng, AI/Parser không đọc được nội dung.
        CorruptedFile,
        // DuplicateResume: When a resume is already exists with the same hash
        DuplicateResume,
        // ServerError: When a resume is processed but the server error
        ServerError,   
    }
}
