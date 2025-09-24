using Data.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Models.Response
{
    public class ServiceResponse
    {
        public SRStatus Status { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
    }
}
