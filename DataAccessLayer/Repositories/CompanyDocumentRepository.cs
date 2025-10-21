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
    public class CompanyDocumentRepository : ICompanyDocumentRepository
    {
        private readonly AICESDbContext _context;

        public CompanyDocumentRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task<CompanyDocument> AddAsync(CompanyDocument document)
        {
            await _context.CompanyDocuments.AddAsync(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<List<CompanyDocument>> AddRangeAsync(List<CompanyDocument> documents)
        {
            await _context.CompanyDocuments.AddRangeAsync(documents);
            await _context.SaveChangesAsync();
            return documents;
        }

        public async Task<List<CompanyDocument>> GetByCompanyIdAsync(int companyId)
        {
            return await _context.CompanyDocuments
                .Where(d => d.CompanyId == companyId && d.IsActive)
                .ToListAsync();
        }

        public async Task DeleteAsync(int docId)
        {
            var document = await _context.CompanyDocuments.FindAsync(docId);
            if (document != null)
            {
                document.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}
