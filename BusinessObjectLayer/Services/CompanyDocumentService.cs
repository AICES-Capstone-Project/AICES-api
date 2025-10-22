using BusinessObjectLayer.IServices;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Data.Entities;
using DataAccessLayer.IRepositories;
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
        private readonly ICompanyDocumentRepository _companyDocumentRepository;
        private readonly Common.CloudinaryHelper _cloudinaryHelper;

        public CompanyDocumentService(
            ICompanyDocumentRepository companyDocumentRepository,
            Common.CloudinaryHelper cloudinaryHelper)
        {
            _companyDocumentRepository = companyDocumentRepository;
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
                        FileUrl = upload.Url,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    documents.Add(document);
                }
            }

            // Save all documents to database
            if (documents.Any())
            {
                await _companyDocumentRepository.AddRangeAsync(documents);
            }

            return documents;
        }

        public async Task<List<CompanyDocument>> GetDocumentsByCompanyIdAsync(int companyId)
        {
            return await _companyDocumentRepository.GetByCompanyIdAsync(companyId);
        }

        public async Task<bool> DeleteDocumentAsync(int docId)
        {
            try
            {
                await _companyDocumentRepository.DeleteAsync(docId);
                return true;
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
