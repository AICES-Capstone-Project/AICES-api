using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class CompanyDocumentService : ICompanyDocumentService
    {
        private readonly IUnitOfWork _uow;
        private readonly Common.CloudinaryHelper _cloudinaryHelper;

        public CompanyDocumentService(
            IUnitOfWork uow,
            Common.CloudinaryHelper cloudinaryHelper)
        {
            _uow = uow;
            _cloudinaryHelper = cloudinaryHelper;
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

                // Upload file to Cloudinary
                var upload = await UploadFileAsync(file, "companies/documents");
                if (upload.Success)
                {
                    var document = new CompanyDocument
                    {
                        CompanyId = companyId,
                        DocumentType = documentType,
                        FileUrl = upload.Url
                    };

                    documents.Add(document);
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

                // Private helper method for uploading files
        private async Task<(bool Success, string? Url, string? ErrorMessage)> UploadFileAsync(IFormFile file, string folder)
        {
            return await _cloudinaryHelper.UploadCompanyDocumentAsync(file);
        }
    }
}
