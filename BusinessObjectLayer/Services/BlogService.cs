using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Request;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.Services
{
    public class BlogService : IBlogService
    {
        private readonly IUnitOfWork _uow;

        public BlogService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ServiceResponse> CreateBlogAsync(BlogRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
                if (userIdClaim == null)
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

                var userId = int.Parse(userIdClaim);
                var blogRepo = _uow.GetRepository<IBlogRepository>();

                var blog = new Blog
                {
                    UserId = userId,
                    Title = request.Title ?? string.Empty,
                    Content = request.Content ?? string.Empty,
                    Slug = GenerateSlug(request.Title ?? string.Empty),
                    ThumbnailUrl = request.ThumbnailUrl
                };

                await blogRepo.AddAsync(blog);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blog created successfully",
                    Data = new BlogResponse
                    {
                        BlogId = blog.BlogId,
                        UserId = blog.UserId,
                        Title = blog.Title,
                        Slug = blog.Slug,
                        Content = blog.Content,
                        ThumbnailUrl = blog.ThumbnailUrl,
                        CreatedAt = blog.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create blog error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while creating blog."
                };
            }
        }

        public async Task<ServiceResponse> GetBlogByIdAsync(int blogId)
        {
            try
            {
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blog = await blogRepo.GetBlogByIdAsync(blogId);

                if (blog == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Blog not found"
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blog retrieved successfully",
                    Data = new BlogResponse
                    {
                        BlogId = blog.BlogId,
                        UserId = blog.UserId,
                        AuthorName = blog.User?.Profile?.FullName,
                        Title = blog.Title,
                        Slug = blog.Slug,
                        Content = blog.Content,
                        ThumbnailUrl = blog.ThumbnailUrl,
                        CreatedAt = blog.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get blog by id error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving blog."
                };
            }
        }

        public async Task<ServiceResponse> GetBlogBySlugAsync(string slug)
        {
            try
            {
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blog = await blogRepo.GetBlogBySlugAsync(slug);

                if (blog == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Blog not found"
                    };
                }

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blog retrieved successfully",
                    Data = new BlogResponse
                    {
                        BlogId = blog.BlogId,
                        UserId = blog.UserId,
                        AuthorName = blog.User?.Profile?.FullName,
                        Title = blog.Title,
                        Slug = blog.Slug,
                        Content = blog.Content,
                        ThumbnailUrl = blog.ThumbnailUrl,
                        CreatedAt = blog.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get blog by slug error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving blog."
                };
            }
        }

        public async Task<ServiceResponse> GetAllBlogsAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blogs = await blogRepo.GetAllBlogsAsync(page, pageSize, search);
                var total = await blogRepo.GetTotalBlogsAsync(search);

                var blogResponses = blogs.Select(b => new BlogResponse
                {
                    BlogId = b.BlogId,
                    UserId = b.UserId,
                    AuthorName = b.User?.Profile?.FullName,
                    Title = b.Title,
                    Slug = b.Slug,
                    Content = b.Content,
                    ThumbnailUrl = b.ThumbnailUrl,
                    CreatedAt = b.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blogs retrieved successfully",
                    Data = new
                    {
                        Blogs = blogResponses,
                        Total = total,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get all blogs error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving blogs."
                };
            }
        }

        public async Task<ServiceResponse> GetMyBlogsAsync(ClaimsPrincipal userClaims, int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
                if (userIdClaim == null)
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

                var userId = int.Parse(userIdClaim);
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blogs = await blogRepo.GetBlogsByUserIdAsync(userId, page, pageSize, search);
                var total = await blogRepo.GetTotalBlogsByUserIdAsync(userId, search);

                var blogResponses = blogs.Select(b => new BlogResponse
                {
                    BlogId = b.BlogId,
                    UserId = b.UserId,
                    AuthorName = b.User?.Profile?.FullName,
                    Title = b.Title,
                    Slug = b.Slug,
                    Content = b.Content,
                    ThumbnailUrl = b.ThumbnailUrl,
                    CreatedAt = b.CreatedAt
                }).ToList();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "My blogs retrieved successfully",
                    Data = new
                    {
                        Blogs = blogResponses,
                        Total = total,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get my blogs error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving blogs."
                };
            }
        }

        public async Task<ServiceResponse> UpdateBlogAsync(int blogId, BlogRequest request, ClaimsPrincipal userClaims)
        {
            try
            {
                var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
                if (userIdClaim == null)
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

                var userId = int.Parse(userIdClaim);
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blog = await blogRepo.GetForUpdateAsync(blogId);

                if (blog == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Blog not found"
                    };
                }

                // Check if user owns this blog
                if (blog.UserId != userId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "You don't have permission to update this blog"
                    };
                }

                // Update blog fields
                if (!string.IsNullOrEmpty(request.Title))
                {
                    blog.Title = request.Title;
                    blog.Slug = GenerateSlug(request.Title);
                }
                if (request.Content != null) blog.Content = request.Content;
                if (request.ThumbnailUrl != null) blog.ThumbnailUrl = request.ThumbnailUrl;

                await blogRepo.UpdateAsync(blog);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blog updated successfully",
                    Data = new BlogResponse
                    {
                        BlogId = blog.BlogId,
                        UserId = blog.UserId,
                        AuthorName = blog.User?.Profile?.FullName,
                        Title = blog.Title,
                        Slug = blog.Slug,
                        Content = blog.Content,
                        ThumbnailUrl = blog.ThumbnailUrl,
                        CreatedAt = blog.CreatedAt
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update blog error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while updating blog."
                };
            }
        }

        public async Task<ServiceResponse> DeleteBlogAsync(int blogId, ClaimsPrincipal userClaims)
        {
            try
            {
                var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
                if (userIdClaim == null)
                    return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "User not authenticated" };

                var userId = int.Parse(userIdClaim);
                var blogRepo = _uow.GetRepository<IBlogRepository>();
                var blog = await blogRepo.GetForUpdateAsync(blogId);

                if (blog == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Blog not found"
                    };
                }

                // Check if user owns this blog
                if (blog.UserId != userId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "You don't have permission to delete this blog"
                    };
                }

                await blogRepo.SoftDeleteAsync(blog);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Blog deleted successfully"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete blog error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while deleting blog."
                };
            }
        }

        private string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title))
                return string.Empty;

            // Convert to lowercase and replace spaces with hyphens
            var slug = title.ToLowerInvariant().Trim();
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
            
            return slug;
        }
    }
}

