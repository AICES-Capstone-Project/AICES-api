using Data.Entities;
using DataAccessLayer.IRepositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class CriteriaRepository : ICriteriaRepository
    {
        private readonly AICESDbContext _context;

        public CriteriaRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(List<Criteria> criteria)
        {
            await _context.Criterias.AddRangeAsync(criteria);
        }

        public async Task DeleteByJobIdAsync(int jobId)
        {
            var toRemove = _context.Criterias.Where(c => c.JobId == jobId).ToList();
            if (toRemove.Count > 0)
            {
                _context.Criterias.RemoveRange(toRemove);
            }
        }
    }
}
