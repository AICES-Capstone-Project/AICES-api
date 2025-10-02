using BCrypt.Net;
using BusinessObjectLayer.IServices;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace BusinessObjectLayer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IProfileService _profileService;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public AuthService(IAuthRepository authRepository, IProfileService profileService, IConfiguration configuration, IMemoryCache cache)
        {
            _authRepository = authRepository;
            _profileService = profileService;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<ServiceResponse> RegisterAsync(string email, string password, string fullName) 
        {
            var existedUser = await _authRepository.GetByEmailAsync(email);
            if (existedUser != null)
            {
                if (existedUser.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Duplicated,
                        Message = "Email is already registered and verified."
                    };
                }
                else
                {
                    var verificationToken = GenerateVerificationToken(email);
                    await SendVerificationEmail(email, verificationToken);
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Email is already registered. Please check your email to verify your account."
                    };
                }
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
                AuthProvider = "Local",
                IsActive = false
            };

            var addedUser = await _authRepository.AddAsync(user);
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId, fullName);

            var newVerificationToken = GenerateVerificationToken(email);
            await SendVerificationEmail(email, newVerificationToken);

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

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var issuer = _configuration.GetSection("JwtConfig:Issuers").Get<string[]>()[0];
            var audience = _configuration.GetSection("JwtConfig:Audiences").Get<string[]>()[0];

            var claims = new[]
            {
                new Claim("email", user.Email),
                new Claim("userId", user.UserId.ToString()),
                new Claim("role", user.Role?.RoleName ?? "Unknown"),
                new Claim("provider", user.AuthProvider ?? "Local"),
                new Claim("fullName", user.Profile?.FullName ?? ""),
                new Claim("phoneNumber", user.Profile?.PhoneNumber ?? ""),
                new Claim("address", user.Profile?.Address ?? ""),
                new Claim("dateOfBirth", user.Profile?.DateOfBirth?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("avatarUrl", user.Profile?.AvatarUrl ?? ""),
            };

            var token = new JwtSecurityToken(
                issuer: issuer,      
                audience: audience,  
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(300),
                signingCredentials: creds);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Login successful",
                Data = new AuthResponse
                {
                    AccessToken = new JwtSecurityTokenHandler().WriteToken(token)
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
                if (user == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "User not found." };

                if (user.IsActive)
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Email already verified. You can now log in." };

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
            var verificationLink = $"{_configuration["AppUrl:ClientUrl"]}/verify-email?token={verificationToken}";
            builder.HtmlBody = $"<h1>Verify Your Account</h1><p>Please click the link below to verify your email:</p><a href='{verificationLink}'>{verificationLink}</a>";
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(emailConfig["SmtpServer"], int.Parse(emailConfig["SmtpPort"]), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(emailConfig["Username"], emailConfig["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task<ServiceResponse> GoogleLoginAsync(string idToken)
        {
            try
            {
                // 1. Validate ID token with Google (secure way)
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings());

                if (!payload.EmailVerified)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Google email not verified."
                    };
                }

                var adminEmail = _configuration["EmailConfig:Username"];

                // 2. Check if user exists by ProviderId or Email
                var user = await _authRepository.GetByEmailAsync(payload.Email);

                if (user == null)
                {
                    int roleId = payload.Email == adminEmail ? 1 : 4;

                    // Create new user
                    user = new User
                    {
                        Email = payload.Email,
                        AuthProvider = "Google",
                        ProviderId = payload.Subject,
                        RoleId = roleId, 
                        IsActive = true
                    };

                    user = await _authRepository.AddAsync(user);

                    // Create profile with Google name & avatar
                    await _profileService.CreateDefaultProfileAsync(user.UserId, payload.Name, payload.Picture);
                }
                else
                {
                    // Nếu đã tồn tại user nhưng là admin email thì ép role về 1
                    if (payload.Email == adminEmail && user.RoleId != 1)
                    {
                        user.RoleId = 1;
                        await _authRepository.UpdateAsync(user);
                    }
                }

                // 3. Build claims
                var claims = new[]
                {
                    new Claim("email", user.Email),
                    new Claim("userId", user.UserId.ToString()),
                    new Claim("role", user.Role?.RoleName ?? "Candidate"),
                    new Claim("provider", user.AuthProvider ?? "Google"),
                    new Claim("providerId", user.ProviderId ?? ""),
                    new Claim("phoneNumber", user.Profile?.PhoneNumber ?? ""),
                    new Claim("address", user.Profile?.Address ?? ""),
                    new Claim("dateOfBirth", user.Profile?.DateOfBirth?.ToString("yyyy-MM-dd") ?? ""),
                    new Claim("avatarUrl", user.Profile?.AvatarUrl ?? ""),

                };

                // 4. Generate JWT
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var issuer = _configuration.GetSection("JwtConfig:Issuers").Get<string[]>()[0];
                var audience = _configuration.GetSection("JwtConfig:Audiences").Get<string[]>()[0];

                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: DateTime.UtcNow.AddMinutes(300),
                    signingCredentials: creds
                );

                var jwt = new JwtSecurityTokenHandler().WriteToken(token);

                // 5. Return success response
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Google login successful",
                    Data = new AuthResponse
                    {
                        AccessToken = jwt
                    }
                };
            }
            catch (InvalidJwtException)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid Google ID token."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google login error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred during Google login."
                };
            }
        }

        public async Task<ServiceResponse> GetCurrentUserInfoAsync(ClaimsPrincipal userClaims)
        {
            var emailClaim = userClaims.FindFirst(ClaimTypes.Email)?.Value
                 ?? userClaims.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(emailClaim))
            {
                throw new UnauthorizedAccessException("Email claim not found in token.");
            }

            var user = await _authRepository.GetByEmailAsync(emailClaim);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found.");
            }

            try
            {
                //var user = await GetCurrentUserAsync(userClaims);
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "User information retrieved successfully",
                    Data = new UserResponse
                    {
                        UserId = user.UserId,
                        Email = user.Email,
                        FullName = user.Profile?.FullName,
                        RoleName = user.Role?.RoleName,
                        AvatarUrl = user.Profile?.AvatarUrl,
                        IsActive = user.IsActive    
                    }
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while retrieving user information."
                };
            }
        }

        public async Task<ServiceResponse> RequestPasswordResetAsync(string email)
        {
            var user = await _authRepository.GetByEmailAsync(email);
            if (user == null || !user.IsActive)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "User not found or account inactive."
                };
            }

            var resetToken = GenerateResetToken(user.Email);
            await SendResetEmail(email, resetToken);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Password reset link has been sent to your email."
            };
        }

        public async Task<ServiceResponse> ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
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
                var email = principal.FindFirst(ClaimTypes.Email)?.Value;

                if (string.IsNullOrEmpty(email))
                {
                    return new ServiceResponse { Status = SRStatus.Error, Message = "Invalid reset token." };
                }

                var user = await _authRepository.GetByEmailAsync(email);
                if (user == null)
                {
                    return new ServiceResponse { Status = SRStatus.Error, Message = "User not found." };
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await _authRepository.UpdateAsync(user);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Password has been reset successfully."
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return new ServiceResponse { Status = SRStatus.Error, Message = "Reset token has expired." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reset password error: {ex.Message}");
                return new ServiceResponse { Status = SRStatus.Error, Message = "Invalid or expired reset token." };
            }
        }

        private string GenerateResetToken(string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtConfig:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("email", email)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15), // Token hết hạn sau 15 phút
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task SendResetEmail(string email, string resetToken)
        {
            var emailConfig = _configuration.GetSection("EmailConfig");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", emailConfig["From"]));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Reset Your Password";

            var builder = new BodyBuilder();
            var resetLink = $"{_configuration["AppUrl:ClientUrl"]}/reset-password?token={resetToken}";
            builder.HtmlBody = $"<h1>Reset Your Password</h1><p>Please click the link below to reset your password:</p><a href='{resetLink}'>{resetLink}</a><p>This link will expire in 15 minutes.</p>";
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(emailConfig["SmtpServer"], int.Parse(emailConfig["SmtpPort"]), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(emailConfig["Username"], emailConfig["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}