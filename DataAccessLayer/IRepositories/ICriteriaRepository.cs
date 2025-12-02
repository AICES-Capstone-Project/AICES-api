using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICriteriaRepository
    {
        Task AddRangeAsync(List<Criteria> criteria);
        Task DeleteByJobIdAsync(int jobId);
    }
}
