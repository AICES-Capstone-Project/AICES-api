using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IJobCategoryRepository
    {
        Task AddJobCategoriesAsync(List<JobCategory> jobCategories);
    }
}
