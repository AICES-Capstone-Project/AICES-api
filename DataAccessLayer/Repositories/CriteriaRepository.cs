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

        public async Task AddCriteriaAsync(List<Criteria> criteria)
        {
            _context.Criterias.AddRange(criteria);
            await _context.SaveChangesAsync();
        }
    }
}
