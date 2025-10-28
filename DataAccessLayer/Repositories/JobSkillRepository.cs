using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class JobSkillRepository : IJobSkillRepository
    {
        private readonly AICESDbContext _context;

        public JobSkillRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<JobSkill>> GetAllAsync()
        {
            return await _context.JobSkills
                .Include(js => js.Job)
                .Include(js => js.Skill)
                .ToListAsync();
        }

        public async Task<JobSkill?> GetByIdAsync(int id)
        {
            return await _context.JobSkills
                .Include(js => js.Job)
                .Include(js => js.Skill)
                .FirstOrDefaultAsync(js => js.JobSkillId == id);
        }

        public async Task<JobSkill> AddAsync(JobSkill jobSkill)
        {
            _context.JobSkills.Add(jobSkill);
            await _context.SaveChangesAsync();
            return jobSkill;
        }

        public async Task UpdateAsync(JobSkill jobSkill)
        {
            _context.JobSkills.Update(jobSkill);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(JobSkill jobSkill)
        {
            _context.JobSkills.Remove(jobSkill);
            await _context.SaveChangesAsync();
        }
    }
}
