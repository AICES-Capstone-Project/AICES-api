using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public class CloudinaryHelper
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryHelper(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        /// <summary>
        /// Upload an image file to Cloudinary with optional transformation
        /// </summary>
        public async Task<(bool Success, string? Url, string? ErrorMessage)> UploadImageAsync(
            IFormFile file, 
            string folder, 
            int? width = null, 
            int? height = null, 
            string? publicId = null)
        {
            try
            {
                // Validate file extension
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return (false, null, "Invalid file type. Only JPG, JPEG, PNG, GIF, WEBP, BMP allowed.");
                }

                // Validate file size (5MB limit)
                const int maxFileSize = 5 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    return (false, null, "File size exceeds 5MB limit.");
                }

                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = folder,
                    PublicId = publicId
                };

                // Apply transformation if dimensions are specified
                if (width.HasValue && height.HasValue)
                {
                    uploadParams.Transformation = new Transformation()
                        .Width(width.Value)
                        .Height(height.Value)
                        .Crop("fill");
                }

                var result = await _cloudinary.UploadAsync(uploadParams);
                
                if (result.Error != null)
                {
                    return (false, null, result.Error.Message);
                }

                return (true, result.SecureUrl.ToString(), null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Upload any file (image or document) to Cloudinary
        /// </summary>
        public async Task<(bool Success, string? Url, string? ErrorMessage)> UploadFileAsync(
            IFormFile file, 
            string folder)
        {
            try
            {
                using var stream = file.OpenReadStream();

                // Determine if it's an image or document
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
                bool isImage = imageExtensions.Contains(extension);

                if (isImage)
                {
                    // Upload as image
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = folder
                    };
                    var result = await _cloudinary.UploadAsync(uploadParams);
                    if (result.Error != null)
                        return (false, null, result.Error.Message);
                    return (true, result.SecureUrl.ToString(), null);
                }
                else
                {
                    // Upload as raw file (PDF, DOC, etc.)
                    var uploadParams = new RawUploadParams()
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = folder
                    };
                    var result = await _cloudinary.UploadAsync(uploadParams);
                    if (result.Error != null)
                        return (false, null, result.Error.Message);
                    return (true, result.SecureUrl.ToString(), null);
                }
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Upload avatar with specific settings (500x500, to avatars folder)
        /// </summary>
        public async Task<(bool Success, string? Url, string? ErrorMessage)> UploadAvatarAsync(
            int userId, 
            IFormFile file)
        {
            var publicId = $"avatars/{userId}_{DateTime.UtcNow.Ticks}";
            return await UploadImageAsync(file, "avatars", 500, 500, publicId);
        }

        /// <summary>
        /// Upload company logo with specific settings (400x400)
        /// </summary>
        public async Task<(bool Success, string? Url, string? ErrorMessage)> UploadCompanyLogoAsync(
            IFormFile file)
        {
            return await UploadImageAsync(file, "companies/logos", 400, 400);
        }

        /// <summary>
        /// Upload company document (PDF, DOCX, DOC, images, and other common formats)
        /// </summary>
        public async Task<(bool Success, string? Url, string? ErrorMessage)> UploadCompanyDocumentAsync(
            IFormFile file)
        {
            try
            {
                // Validate file extension - includes images, documents, spreadsheets, presentations, and archives
                var allowedExtensions = new[] 
                { 
                    // Images
                    ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg",
                    // Documents
                    ".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt",
                    // Spreadsheets
                    ".xls", ".xlsx", ".csv", ".ods",
                    // Presentations
                    ".ppt", ".pptx", ".odp",
                    // Archives
                    ".zip", ".rar", ".7z"
                };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(extension))
                {
                    return (false, null, "Invalid file type. Allowed formats: images (JPG, PNG, GIF, etc.), documents (PDF, DOC, DOCX, TXT, etc.), spreadsheets (XLS, XLSX, CSV), presentations (PPT, PPTX), and archives (ZIP, RAR, 7Z).");
                }

                // Validate file size (20MB limit for documents and other files)
                const int maxFileSize = 20 * 1024 * 1024;
                if (file.Length > maxFileSize)
                {
                    return (false, null, "File size exceeds 20MB limit.");
                }

                return await UploadFileAsync(file, "companies/documents");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }
    }
}
