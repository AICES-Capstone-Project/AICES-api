using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Common
{
    public static class HashUtils
    {
        /// <summary>
        /// Compute the hash of a file
        /// </summary>
        /// <param name="file">The file to compute the hash of</param>
        /// <returns>The hash of the file</returns>
        public static string ComputeFileHash(IFormFile file)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = file.OpenReadStream())
            {
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}
