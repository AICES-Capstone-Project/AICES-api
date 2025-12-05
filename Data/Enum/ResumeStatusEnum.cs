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
        Pending,
        Completed,
        Failed,
        Timeout,
        Invalid,
        Canceled
    }
}
