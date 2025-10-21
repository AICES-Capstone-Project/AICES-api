using Data.Entities;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICompanyDocumentService
    {
        Task<List<CompanyDocument>> UploadAndSaveDocumentsAsync(
            int companyId, 
            List<IFormFile> documentFiles, 
            List<string>? documentTypes);
        
        Task<List<CompanyDocument>> GetDocumentsByCompanyIdAsync(int companyId);
        Task<bool> DeleteDocumentAsync(int docId);
    }
}
