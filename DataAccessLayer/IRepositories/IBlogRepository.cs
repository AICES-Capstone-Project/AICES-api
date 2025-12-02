using Data.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.IRepositories
{
    public interface IBlogRepository
    {
        Task AddAsync(Blog blog);
        Task<Blog?> GetBlogByIdAsync(int blogId);
        Task<Blog?> GetBlogBySlugAsync(string slug);
        Task<Blog?> GetForUpdateAsync(int blogId);
        Task<List<Blog>> GetAllBlogsAsync(int page, int pageSize, string? search = null);
        Task<int> GetTotalBlogsAsync(string? search = null);
        Task<List<Blog>> GetBlogsByUserIdAsync(int userId, int page, int pageSize, string? search = null);
        Task<int> GetTotalBlogsByUserIdAsync(int userId, string? search = null);
        Task UpdateAsync(Blog blog);
        Task SoftDeleteAsync(Blog blog);
    }
}

