using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IDashboardRepository
    {
        Task<List<(int CategoryId, string CategoryName, int SpecializationId, string SpecializationName, int ResumeCount)>> GetTopCategorySpecByResumeCountAsync(int companyId, int top = 10);
    }
}

