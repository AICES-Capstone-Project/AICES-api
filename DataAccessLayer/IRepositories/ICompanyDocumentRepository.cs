using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface ICompanyDocumentRepository
    {
        Task<CompanyDocument> AddAsync(CompanyDocument document);
        Task<List<CompanyDocument>> AddRangeAsync(List<CompanyDocument> documents);
        Task<List<CompanyDocument>> GetByCompanyIdAsync(int companyId);
        Task DeleteAsync(int docId);
    }
}
