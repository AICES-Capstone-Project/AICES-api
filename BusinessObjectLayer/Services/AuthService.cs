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
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;

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

        public async Task<ServiceResponse> RegisterAsync(string email, string password) // Loại bỏ roleId
        {
            if (await _authRepository.EmailExistsAsync(email))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Duplicated,
                    Message = "Email already exists."
                };
            }

           
            int roleId = 4;

    
            if (!await _authRepository.RoleExistsAsync(roleId))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "Default role not found. Please check database seed data."
                };
            }

            var user = new User
            {
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = roleId,
                IsActive = false
            };

            var addedUser = await _authRepository.AddAsync(user);
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId);

            var verificationToken = GenerateVerificationToken(email);
            await SendVerificationEmail(email, verificationToken);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Registration successful. Please check your email to verify your account."
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
                new Claim("avatarUrl", user.Profile?.AvatarUrl ?? ""),
                new Claim("exp", new DateTimeOffset(DateTime.UtcNow.AddMinutes(300)).ToUnixTimeSeconds().ToString()),
                new Claim("iss", _configuration["JwtConfig:Issuer"] ?? "AICES"),
                new Claim("aud", _configuration["JwtConfig:Audience"] ?? "AICESApp")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtConfig:Issuer"],
                audience: _configuration["JwtConfig:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(300),
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

        public async Task<ServiceResponse> VerifyEmailAsync(string token)
        {
            try
            {
                Console.WriteLine("Starting token validation...");
                Console.WriteLine($"Received token length: {token?.Length ?? 0}");

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                var email = principal.FindFirst(ClaimTypes.Email)?.Value; // Sử dụng ClaimTypes.Email thay vì "email"

                Console.WriteLine($"Decoded email from token: {email ?? "NULL"}");

                // Log tất cả claims để kiểm tra
                foreach (var claim in principal.Claims)
                {
                    Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                }

                if (string.IsNullOrEmpty(email))
                {
                    Console.WriteLine("No email claim in token.");
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Invalid token." };
                }

                var user = await _authRepository.GetByEmailAsync(email);
                if (user == null || user.IsActive)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "User not found or already verified." };

                user.IsActive = true;
                await _authRepository.UpdateAsync(user);

                Console.WriteLine($"User {user.UserId} activated successfully.");

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Email verified successfully. You can now log in."
                };
            }
            catch (SecurityTokenExpiredException ex)
            {
                Console.WriteLine($"Token expired: {ex.Message}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "Token has expired." };
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                Console.WriteLine($"Invalid signature: {ex.Message}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "Invalid token signature. Check JwtConfig:Key." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Verification error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "Invalid or expired verification token." };
            }
        }

        private string GenerateVerificationToken(string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("email", email)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task SendVerificationEmail(string email, string verificationToken)
        {
            var emailConfig = _configuration.GetSection("EmailConfig");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", emailConfig["From"]));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verify Your Email";

            var builder = new BodyBuilder();
            var verificationLink = $"{_configuration["AppUrl"]}/api/auth/verify-email?token={verificationToken}";
            builder.HtmlBody = $"<h1>Verify Your Account</h1><p>Please click the link below to verify your email:</p><a href='{verificationLink}'>{verificationLink}</a>";
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(emailConfig["SmtpServer"], int.Parse(emailConfig["SmtpPort"]), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(emailConfig["Username"], emailConfig["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}