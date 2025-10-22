using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobRepository
    {
        Task<Job> CreateJobAsync(Job job);
        Task<Job?> GetJobByIdAsync(int jobId);
        Task<List<Job>> GetJobsAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalJobsAsync(string? search = null);
    }
}


