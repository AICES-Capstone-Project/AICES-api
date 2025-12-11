using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DataAccessLayer.Repositories
{
    public class CandidateRepository : ICandidateRepository
    {
        private readonly AICESDbContext _context;

        public CandidateRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<Candidate> CreateAsync(Candidate candidate)
        {
            await _context.Candidates.AddAsync(candidate);
            return candidate;
        }

        public async Task<Candidate?> GetByResumeIdAsync(int resumeId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsActive && c.Resumes.Any(r => r.ResumeId == resumeId));
        }

        public Task UpdateAsync(Candidate candidate)
        {
            _context.Candidates.Update(candidate);
            return Task.CompletedTask;
        }

        public async Task<Candidate?> GetByIdAsync(int id)
        {
            return await _context.Candidates
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.IsActive && c.CandidateId == id);
        }

        public async Task<List<Candidate>> GetPagedAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.FullName.Contains(search) ||
                    c.Email.Contains(search) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalAsync(string? search = null)
        {
            var query = _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.FullName.Contains(search) ||
                    c.Email.Contains(search) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            return await query.CountAsync();
        }

        public Task SoftDeleteAsync(Candidate candidate)
        {
            candidate.IsActive = false;
            _context.Candidates.Update(candidate);
            return Task.CompletedTask;
        }

        public async Task<List<Candidate>> GetCandidatesWithScoresByJobIdAsync(int jobId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive && c.Resumes.Any(r =>
                    r.IsActive &&
                    r.ResumeApplications.Any(ra => ra.JobId == jobId && ra.IsActive)))
                .Include(c => c.Resumes.Where(r => r.IsActive))
                    .ThenInclude(r => r.ResumeApplications.Where(ra => ra.IsActive && ra.JobId == jobId))
                .ToListAsync();
        }

        public async Task<List<Candidate>> GetCandidatesWithFullDetailsByJobIdAsync(int jobId)
        {
            return await _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive && c.Resumes.Any(r =>
                    r.IsActive &&
                    r.ResumeApplications.Any(ra => ra.JobId == jobId && ra.IsActive)))
                .Include(c => c.Resumes.Where(r => r.IsActive))
                    .ThenInclude(r => r.ResumeApplications.Where(ra => ra.IsActive && ra.JobId == jobId))
                        .ThenInclude(ra => ra.Job)
                .ToListAsync();
        }

        public async Task<List<Candidate>> GetPagedByCompanyIdAsync(int companyId, int page, int pageSize, string? search = null)
        {
            // Get distinct candidate IDs that have resumes in this company OR applications for company jobs
            var candidateIdsFromResumes = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && r.CompanyId == companyId && r.CandidateId != null)
                .Select(r => r.CandidateId!.Value)
                .Distinct()
                .ToListAsync();

            var candidateIdsFromApplications = await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && ra.Job.CompanyId == companyId && ra.CandidateId != null)
                .Select(ra => ra.CandidateId!.Value)
                .Distinct()
                .ToListAsync();

            var allCandidateIds = candidateIdsFromResumes.Union(candidateIdsFromApplications).Distinct().ToList();

            if (!allCandidateIds.Any())
            {
                return new List<Candidate>();
            }

            var query = _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive && allCandidateIds.Contains(c.CandidateId));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.FullName.Contains(search) ||
                    c.Email.Contains(search) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalByCompanyIdAsync(int companyId, string? search = null)
        {
            // Get distinct candidate IDs that have resumes in this company OR applications for company jobs
            var candidateIdsFromResumes = await _context.Resumes
                .AsNoTracking()
                .Where(r => r.IsActive && r.CompanyId == companyId && r.CandidateId != null)
                .Select(r => r.CandidateId!.Value)
                .Distinct()
                .ToListAsync();

            var candidateIdsFromApplications = await _context.ResumeApplications
                .AsNoTracking()
                .Where(ra => ra.IsActive && ra.Job.CompanyId == companyId && ra.CandidateId != null)
                .Select(ra => ra.CandidateId!.Value)
                .Distinct()
                .ToListAsync();

            var allCandidateIds = candidateIdsFromResumes.Union(candidateIdsFromApplications).Distinct().ToList();

            if (!allCandidateIds.Any())
            {
                return 0;
            }

            var query = _context.Candidates
                .AsNoTracking()
                .Where(c => c.IsActive && allCandidateIds.Contains(c.CandidateId));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(c =>
                    c.FullName.Contains(search) ||
                    c.Email.Contains(search) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<bool> HasResumeOrApplicationInCompanyAsync(int candidateId, int companyId)
        {
            var hasResumeInCompany = await _context.Resumes
                .AsNoTracking()
                .AnyAsync(r => r.IsActive && r.CompanyId == companyId && r.CandidateId == candidateId);

            if (hasResumeInCompany)
                return true;

            var hasApplicationForCompany = await _context.ResumeApplications
                .AsNoTracking()
                .AnyAsync(ra => ra.IsActive && ra.CandidateId == candidateId && ra.Job.CompanyId == companyId);

            return hasApplicationForCompany;
        }
    }
}
