using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum ApplicationErrorEnum
    {
        // None: No error
        None,
        // InvalidJobData: When the job data provided to AI is invalid or insufficient
        InvalidJobData,
        // JobTitleNotMatched: When the candidate's experience/title does not match the job requirements
        JobTitleNotMatched,
        // TechnicalError: When there is a technical failure during AI processing (timeout, AI service down)
        TechnicalError,
        // InvalidResumeData: When the resume content is not valid or cannot be parsed as a resume
        InvalidResumeData,
    }
}

