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
                .AsNoTracking()
                .Include(js => js.Job)
                .Include(js => js.Skill)
                .ToListAsync();
        }

        public async Task<JobSkill?> GetByJobIdAndSkillIdAsync(int jobId, int skillId)
        {
            return await _context.JobSkills
                .AsNoTracking()
                .Include(js => js.Job)
                .Include(js => js.Skill)
                .FirstOrDefaultAsync(js => js.JobId == jobId && js.SkillId == skillId);
        }

        public async Task<JobSkill?> GetForUpdateByJobIdAndSkillIdAsync(int jobId, int skillId)
        {
            return await _context.JobSkills
                .Include(js => js.Job)
                .Include(js => js.Skill)
                .FirstOrDefaultAsync(js => js.JobId == jobId && js.SkillId == skillId);
        }

        public async Task AddAsync(JobSkill jobSkill)
        {
            await _context.JobSkills.AddAsync(jobSkill);
        }

        public void Update(JobSkill jobSkill)
        {
            _context.JobSkills.Update(jobSkill);
        }

        public void Delete(JobSkill jobSkill)
        {
            _context.JobSkills.Remove(jobSkill);
        }

        public async Task<List<JobSkill>> GetByJobIdAsync(int jobId)
        {
            return await _context.JobSkills
                .AsNoTracking()
                .Where(js => js.JobId == jobId)
                .ToListAsync();
        }

        public async Task AddRangeAsync(List<JobSkill> jobSkills)
        {
            await _context.JobSkills.AddRangeAsync(jobSkills);
        }

        public async Task DeleteByJobIdAsync(int jobId)
        {
            var toRemove = await _context.JobSkills.Where(js => js.JobId == jobId).ToListAsync();
            if (toRemove.Count > 0)
            {
                _context.JobSkills.RemoveRange(toRemove);
            }
        }

        // Legacy methods for backward compatibility (used by JobSkillService)
        // These include SaveChanges for services not using UoW
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
