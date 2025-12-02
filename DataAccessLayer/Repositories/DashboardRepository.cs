using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AICESDbContext _context;

        public DashboardRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<List<(int CategoryId, string CategoryName, int SpecializationId, string SpecializationName, int ResumeCount)>> GetTopCategorySpecByResumeCountAsync(int companyId, int top = 10)
        {
            var result = await (from pr in _context.ParsedResumes
                               join j in _context.Jobs on pr.JobId equals j.JobId
                               join s in _context.Specializations on j.SpecializationId equals s.SpecializationId
                               join c in _context.Categories on s.CategoryId equals c.CategoryId
                               where pr.CompanyId == companyId 
                                  && pr.IsActive 
                                  && j.IsActive 
                                  && j.SpecializationId != null
                               group pr by new { c.CategoryId, CategoryName = c.Name, s.SpecializationId, SpecializationName = s.Name } into g
                               select new
                               {
                                   g.Key.CategoryId,
                                   g.Key.CategoryName,
                                   g.Key.SpecializationId,
                                   g.Key.SpecializationName,
                                   ResumeCount = g.Count()
                               })
                               .OrderByDescending(x => x.ResumeCount)
                               .Take(top)
                               .ToListAsync();

            return result.Select(x => (x.CategoryId, x.CategoryName, x.SpecializationId, x.SpecializationName, x.ResumeCount)).ToList();
        }
    }
}

