using Data.Entities;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Data.Enum;

namespace BusinessObjectLayer.Services.Auth
{
    public class TokenService : ITokenService
    {
        private readonly IAuthRepository _authRepository;

        public TokenService(IAuthRepository authRepository)
        {
            _authRepository = authRepository;
        }

        private static string GetEnvOrThrow(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing environment variable: {key}");
            }
            return value;
        }

        private async Task<string> GetUserProviderAsync(int userId)
        {
            var googleProvider = await _authRepository.GetLoginProviderAsync(userId, "Google");
            if (googleProvider != null) return "Google";

            var localProvider = await _authRepository.GetLoginProviderAsync(userId, "Local");
            if (localProvider != null) return "Local";

            return "Unknown";
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        public async Task<AuthTokenResponse> GenerateTokensAsync(User user)
        {
            var userProvider = await GetUserProviderAsync(user.UserId);
            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "Candidate"),
                new Claim("provider", userProvider),
                new Claim("fullName", user.Profile?.FullName ?? ""),
                new Claim("phoneNumber", user.Profile?.PhoneNumber ?? ""),
                new Claim("address", user.Profile?.Address ?? ""),
                new Claim("dateOfBirth", user.Profile?.DateOfBirth?.ToString("yyyy-MM-dd") ?? ""),
                new Claim("avatarUrl", user.Profile?.AvatarUrl ?? ""),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEnvOrThrow("JWTCONFIG__KEY")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var issuer = GetEnvOrThrow("JWTCONFIG__ISSUERS__0");
            var audience = GetEnvOrThrow("JWTCONFIG__AUDIENCES__0");
            var expiryMins = int.Parse(Environment.GetEnvironmentVariable("JWTCONFIG__TOKENVALIDITYMINS") ?? "60"); // 1 hour

            var accessToken = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMins),
                signingCredentials: creds
            );

            var accessTokenString = new JwtSecurityTokenHandler().WriteToken(accessToken);
            var refreshTokenString = GenerateRefreshToken();

            // Save refresh token to database
            var refreshToken = new RefreshToken
            {
                UserId = user.UserId,
                Token = refreshTokenString,
                ExpiryDate = DateTime.UtcNow.AddDays(7), // 7 days
                IsActive = true
            };

            await _authRepository.AddRefreshTokenAsync(refreshToken);

            return new AuthTokenResponse
            {
                AccessToken = accessTokenString,
                RefreshToken = refreshTokenString
            };
        }

        public async Task<ServiceResponse> RefreshTokensAsync(string refreshToken)
        {
            try
            {
                var storedToken = await _authRepository.GetRefreshTokenAsync(refreshToken);
                if (storedToken == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Invalid refresh token."
                    };
                }

                if (!storedToken.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Refresh token has been revoked."
                    };
                }

                if (storedToken.ExpiryDate < DateTime.UtcNow)
                {
                    storedToken.IsActive = false;
                    await _authRepository.UpdateRefreshTokenAsync(storedToken);

                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Refresh token has expired."
                    };
                }

                if (!storedToken.User.IsActive)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User account is inactive."
                    };
                }

                await _authRepository.RevokeAllRefreshTokensAsync(storedToken.UserId);
                var tokens = await GenerateTokensAsync(storedToken.User);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Tokens refreshed successfully",
                    Data = tokens
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Refresh token error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while refreshing tokens."
                };
            }
        }

        public string GenerateVerificationToken(string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEnvOrThrow("JWTCONFIG__KEY")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, email)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateResetToken(string email)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEnvOrThrow("JWTCONFIG__KEY")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, email)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15), // Token expires after 15 minutes
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEnvOrThrow("JWTCONFIG__KEY")));
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
            return principal;
        }
    }
}

