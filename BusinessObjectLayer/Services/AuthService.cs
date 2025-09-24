using BCrypt.Net;
using Data.Entities;
using DataAccessLayer.IRepositories;
using BusinessObjectLayer.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Data.Models.Response;
using Data.Enum;

namespace BusinessObjectLayer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IProfileService _profileService;
        private readonly IConfiguration _configuration;

        public AuthService(IAuthRepository authRepository, IProfileService profileService, IConfiguration configuration)
        {
            _authRepository = authRepository;
            _profileService = profileService;
            _configuration = configuration;
        }

        public async Task<ServiceResponse> RegisterAsync(string email, string password, int roleId)
        {
            if (await _authRepository.EmailExistsAsync(email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Email already exists."
                };
            }

            var user = new User
            {
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = 4 // Mặc định Candidate
            };

            var addedUser = await _authRepository.AddAsync(user);

            // tạo profile mặc định sau khi đăng ký
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Registration successful.",
            };
        }

        public async Task<ServiceResponse> LoginAsync(string email, string password)
        {
            var user = await _authRepository.GetByEmailAsync(email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password) || !user.IsActive)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid email, password, or account inactive."
                };
            }


            if (user.Profile == null)
            {
                await _profileService.CreateDefaultProfileAsync(user.UserId);
                user = await _authRepository.GetByEmailAsync(email); 
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

 
            var claims = new[]
            {
                new Claim("email", user.Email),
                new Claim("userId", user.UserId.ToString()),
                new Claim("role", user.Role?.RoleName ?? "Unknown"),
                new Claim("fullName", user.Profile?.FullName ?? ""),
                new Claim("phoneNumber", user.Profile?.PhoneNumber ?? ""),
                new Claim("address", user.Profile?.Address ?? ""),
                new Claim("dateOfBirth", user.Profile?.DateOfBirth?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("exp", new DateTimeOffset(DateTime.UtcNow.AddMinutes(60)).ToUnixTimeSeconds().ToString()),
                new Claim("iss", _configuration["JwtConfig:Issuer"] ?? "AICES"),
                new Claim("aud", _configuration["JwtConfig:Audience"] ?? "AICESApp")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtConfig:Issuer"],
                audience: _configuration["JwtConfig:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: creds);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Login successful",
                Data = new AuthResponse
                {
                    accessToken = new JwtSecurityTokenHandler().WriteToken(token),
                    UserId = user.UserId,
                    RoleName = user.Role?.RoleName
                }
            };
        }
    }
}