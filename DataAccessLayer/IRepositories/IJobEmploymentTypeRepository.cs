using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobEmploymentTypeRepository
    {
        Task AddJobEmploymentTypesAsync(List<JobEmploymentType> jobEmploymentTypes);
        Task DeleteByJobIdAsync(int jobId);
    }
}
