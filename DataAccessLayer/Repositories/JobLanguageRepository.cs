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
    public class JobLanguageRepository : IJobLanguageRepository
    {
        private readonly AICESDbContext _context;

        public JobLanguageRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<JobLanguage>> GetAllAsync()
        {
            return await _context.JobLanguages
                .AsNoTracking()
                .Include(jl => jl.Job)
                .Include(jl => jl.Language)
                .ToListAsync();
        }

        public async Task<JobLanguage?> GetByJobIdAndLanguageIdAsync(int jobId, int languageId)
        {
            return await _context.JobLanguages
                .AsNoTracking()
                .Include(jl => jl.Job)
                .Include(jl => jl.Language)
                .FirstOrDefaultAsync(jl => jl.JobId == jobId && jl.LanguageId == languageId);
        }

        public async Task<JobLanguage?> GetForUpdateByJobIdAndLanguageIdAsync(int jobId, int languageId)
        {
            return await _context.JobLanguages
                .Include(jl => jl.Job)
                .Include(jl => jl.Language)
                .FirstOrDefaultAsync(jl => jl.JobId == jobId && jl.LanguageId == languageId);
        }

        public async Task AddAsync(JobLanguage jobLanguage)
        {
            await _context.JobLanguages.AddAsync(jobLanguage);
        }

        public void Update(JobLanguage jobLanguage)
        {
            _context.JobLanguages.Update(jobLanguage);
        }

        public void Delete(JobLanguage jobLanguage)
        {
            _context.JobLanguages.Remove(jobLanguage);
        }

        public async Task<List<JobLanguage>> GetByJobIdAsync(int jobId)
        {
            return await _context.JobLanguages
                .AsNoTracking()
                .Where(jl => jl.JobId == jobId)
                .ToListAsync();
        }

        public async Task AddRangeAsync(List<JobLanguage> jobLanguages)
        {
            await _context.JobLanguages.AddRangeAsync(jobLanguages);
        }

        public async Task DeleteByJobIdAsync(int jobId)
        {
            var toRemove = await _context.JobLanguages.Where(jl => jl.JobId == jobId).ToListAsync();
            if (toRemove.Count > 0)
            {
                _context.JobLanguages.RemoveRange(toRemove);
            }
        }

        // Legacy methods for backward compatibility
        public async Task UpdateAsync(JobLanguage jobLanguage)
        {
            _context.JobLanguages.Update(jobLanguage);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(JobLanguage jobLanguage)
        {
            _context.JobLanguages.Remove(jobLanguage);
            await _context.SaveChangesAsync();
        }
    }
}

