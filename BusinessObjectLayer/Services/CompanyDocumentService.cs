using BusinessObjectLayer.IServices;
using BusinessObjectLayer.Common;
using Data.Entities;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanyDocumentService : ICompanyDocumentService
    {
        private readonly IUnitOfWork _uow;
        private readonly GoogleCloudStorageHelper _storageHelper;

        public CompanyDocumentService(
            IUnitOfWork uow,
            GoogleCloudStorageHelper storageHelper)
        {
            _uow = uow;
            _storageHelper = storageHelper;
        }

        public async Task<List<CompanyDocument>> UploadAndSaveDocumentsAsync(
            int companyId,
            List<IFormFile> documentFiles,
            List<string>? documentTypes)
        {
            var documents = new List<CompanyDocument>();

            if (documentFiles == null || !documentFiles.Any())
                return documents;

            for (int i = 0; i < documentFiles.Count; i++)
            {
                var file = documentFiles[i];
                var documentType = documentTypes != null && i < documentTypes.Count
                    ? documentTypes[i]
                    : "General";

                // Upload file to Google Cloud Storage
                var uploadResult = await _storageHelper.UploadFileAsync(file, "companies/documents");
                if (uploadResult.Status == Data.Enum.SRStatus.Success && uploadResult.Data != null)
                {
                    // Extract URL from ServiceResponse.Data
                    dynamic? data = uploadResult.Data;
                    string? url = data?.Url?.ToString();
                    
                    if (!string.IsNullOrEmpty(url))
                    {
                        var document = new CompanyDocument
                        {
                            CompanyId = companyId,
                            DocumentType = documentType,
                            FileUrl = url
                        };

                        documents.Add(document);
                    }
                }
            }

            // Save all documents to database
            if (documents.Any())
            {
                var companyDocumentRepo = _uow.GetRepository<ICompanyDocumentRepository>();
                await companyDocumentRepo.AddRangeAsync(documents);
                await _uow.SaveChangesAsync();
            }

            return documents;
        }

        public async Task<List<CompanyDocument>> GetDocumentsByCompanyIdAsync(int companyId)
        {
            var companyDocumentRepo = _uow.GetRepository<ICompanyDocumentRepository>();
            return await companyDocumentRepo.GetByCompanyIdAsync(companyId);
        }

        public async Task<bool> DeleteDocumentAsync(int docId)
        {
            try
            {
                await _uow.BeginTransactionAsync();
                try
                {
                    var companyDocumentRepo = _uow.GetRepository<ICompanyDocumentRepository>();
                    await companyDocumentRepo.DeleteAsync(docId);
                    await _uow.CommitTransactionAsync();
                    return true;
                }
                catch
                {
                    await _uow.RollbackTransactionAsync();
                    throw;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
