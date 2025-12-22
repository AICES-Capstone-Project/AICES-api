using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Enum
{
    public enum ApplicationStatusEnum
    {
        // Pending: When a resume application is pending
        Pending,
        // Failed: When a resume application is failed
        Failed,
        // Reviewed: When a resume application is reviewed
        Reviewed,
        // Shortlisted: When a resume application is shortlisted
        Shortlisted,
        // Interview: When a resume application is invited for interview
        Interview,
        // Rejected: When a resume application is rejected
        Rejected,
        // Hired: When a resume application is hired
        Hired,
    }
}

