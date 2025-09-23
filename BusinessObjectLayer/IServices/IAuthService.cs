using Data.Entities;

using Data.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjectLayer.IServices
{
    public interface IAuthService
    {
        Task<string> RegisterAsync(string email, string password, int roleId); 
        Task<AuthResponse> LoginAsync(string email, string password);
    }
}
