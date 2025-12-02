using Data.Entities;
using DataAccessLayer.IRepositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Repositories
{
    public class BlogRepository : IBlogRepository
    {
        private readonly AICESDbContext _context;

        public BlogRepository(AICESDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Blog blog)
        {
            await _context.Blogs.AddAsync(blog);
        }

        public async Task<Blog?> GetBlogByIdAsync(int blogId)
        {
            return await _context.Blogs
                .AsNoTracking()
                .Include(b => b.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(b => b.BlogId == blogId && b.IsActive);
        }

        public async Task<Blog?> GetBlogBySlugAsync(string slug)
        {
            return await _context.Blogs
                .AsNoTracking()
                .Include(b => b.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(b => b.Slug == slug && b.IsActive);
        }

        public async Task<Blog?> GetForUpdateAsync(int blogId)
        {
            return await _context.Blogs
                .Include(b => b.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(b => b.BlogId == blogId && b.IsActive);
        }

        public async Task<List<Blog>> GetAllBlogsAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.Blogs
                .AsNoTracking()
                .Include(b => b.User)
                    .ThenInclude(u => u.Profile)
                .Where(b => b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) || 
                                       (b.Content != null && b.Content.Contains(search)));
            }

            return await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalBlogsAsync(string? search = null)
        {
            var query = _context.Blogs
                .AsNoTracking()
                .Where(b => b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) || 
                                       (b.Content != null && b.Content.Contains(search)));
            }

            return await query.CountAsync();
        }

        public async Task<List<Blog>> GetBlogsByUserIdAsync(int userId, int page, int pageSize, string? search = null)
        {
            var query = _context.Blogs
                .AsNoTracking()
                .Include(b => b.User)
                    .ThenInclude(u => u.Profile)
                .Where(b => b.UserId == userId && b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) || 
                                       (b.Content != null && b.Content.Contains(search)));
            }

            return await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalBlogsByUserIdAsync(int userId, string? search = null)
        {
            var query = _context.Blogs
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => b.Title.Contains(search) || 
                                       (b.Content != null && b.Content.Contains(search)));
            }

            return await query.CountAsync();
        }

        public void UpdateBlog(Blog blog)
        {
            _context.Blogs.Update(blog);
        }

        public void SoftDeleteBlog(Blog blog)
        {
            blog.IsActive = false;
            _context.Blogs.Update(blog);
        }
    }
}

