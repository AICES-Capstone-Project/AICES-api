using Data.Models.Request;
using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IBlogService
    {
        Task<ServiceResponse> CreateBlogAsync(BlogRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> GetBlogByIdAsync(int blogId);
        Task<ServiceResponse> GetBlogBySlugAsync(string slug);
        Task<ServiceResponse> GetAllBlogsAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> GetMyBlogsAsync(ClaimsPrincipal userClaims, int page = 1, int pageSize = 10, string? search = null);
        Task<ServiceResponse> UpdateBlogAsync(int blogId, BlogRequest request, ClaimsPrincipal userClaims);
        Task<ServiceResponse> DeleteBlogAsync(int blogId, ClaimsPrincipal userClaims);
    }
}

