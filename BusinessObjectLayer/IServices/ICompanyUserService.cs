using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface ICompanyUserService
    {
        Task<ServiceResponse> CreateDefaultCompanyUserAsync(int userId);
    }
}