using Data.Entities;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
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
        private readonly ITokenRepository _tokenRepository;
        private readonly IUnitOfWork _uow;

        public TokenService(IAuthRepository authRepository, ITokenRepository tokenRepository, IUnitOfWork uow)
        {
            _authRepository = authRepository;
            _tokenRepository = tokenRepository;
            _uow = uow;
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
            var googleProvider = await _authRepository.GetLoginProviderAsync(userId, AuthProviderEnum.Google);
            if (googleProvider != null) return AuthProviderEnum.Google.ToString();

            var githubProvider = await _authRepository.GetLoginProviderAsync(userId, AuthProviderEnum.GitHub);
            if (githubProvider != null) return AuthProviderEnum.GitHub.ToString();

            var localProvider = await _authRepository.GetLoginProviderAsync(userId, AuthProviderEnum.Local);
            if (localProvider != null) return AuthProviderEnum.Local.ToString();

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
			
			// Generate a new session id for this login/refresh
			var sessionId = Guid.NewGuid().ToString("N");
			var expiryMins = int.Parse(GetEnvOrThrow("JWTCONFIG__TOKENVALIDITYMINS"));
			var sessionExpiry = DateTime.UtcNow.AddMinutes(expiryMins);

			var claims = new[]
			{
				new Claim(ClaimTypes.Email, user.Email),
				new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
				new Claim(ClaimTypes.Role, user.Role?.RoleName ?? string.Empty),
				new Claim("provider", userProvider),
				new Claim("fullName", user.Profile?.FullName ?? string.Empty),
				new Claim("phoneNumber", user.Profile?.PhoneNumber ?? string.Empty),
				new Claim("address", user.Profile?.Address ?? string.Empty),
				new Claim("dateOfBirth", user.Profile?.DateOfBirth?.ToString("yyyy-MM-dd") ?? string.Empty),
				new Claim("avatarUrl", user.Profile?.AvatarUrl ?? string.Empty),
				new Claim("sid", sessionId) // session id for single-login enforcement
			};

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetEnvOrThrow("JWTCONFIG__KEY")));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var issuer = GetEnvOrThrow("JWTCONFIG__ISSUERS__0");
			var audience = GetEnvOrThrow("JWTCONFIG__AUDIENCES__0");

			var accessToken = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				expires: sessionExpiry,
				signingCredentials: creds
			);

            var accessTokenString = new JwtSecurityTokenHandler().WriteToken(accessToken);
			var refreshTokenString = GenerateRefreshToken();

			// Save refresh token to database
			var refreshToken = new RefreshToken
			{
				UserId = user.UserId,
				Token = refreshTokenString,
				ExpiryDate = DateTime.UtcNow.AddDays(7) // 7 days
			};

			await _tokenRepository.AddRefreshTokenAsync(refreshToken);

			// Update user's current session info so old tokens become invalid immediately
			var userToUpdate = await _authRepository.GetForUpdateByIdAsync(user.UserId);
			if (userToUpdate != null)
			{
				userToUpdate.CurrentSessionId = sessionId;
				userToUpdate.CurrentSessionExpiry = sessionExpiry;
				await _authRepository.UpdateAsync(userToUpdate);
			}

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
                var storedToken = await _tokenRepository.GetRefreshTokenAsync(refreshToken);
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
                    await _tokenRepository.UpdateRefreshTokenAsync(storedToken);
                    await _uow.SaveChangesAsync();

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

                await _tokenRepository.RevokeAllRefreshTokensAsync(storedToken.UserId);
                await _uow.SaveChangesAsync();
                var tokens = await GenerateTokensAsync(storedToken.User);
                await _uow.SaveChangesAsync(); // Save the new refresh token to database

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

