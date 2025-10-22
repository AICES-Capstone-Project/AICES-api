using BCrypt.Net;
using BusinessObjectLayer.IServices;
using BusinessObjectLayer.IServices.Auth;
using BusinessObjectLayer.Services.Auth.Models;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace BusinessObjectLayer.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IProfileService _profileService;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly ICompanyUserService _companyUserService;

        public AuthService(
            IAuthRepository authRepository,
            IProfileService profileService,
            ITokenService tokenService,
            IEmailService emailService,
            ICompanyUserService companyUserService)
        {
            _authRepository = authRepository;
            _profileService = profileService;
            _tokenService = tokenService;
            _emailService = emailService;
            _companyUserService = companyUserService;
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

        private static bool IsAdminEmail(string email)
        {
            var adminEnv = GetEnvOrThrow("EMAILCONFIG__USERNAME");
            var configured = (adminEnv ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .ToHashSet();

            var legacy = GetEnvOrThrow("EMAILCONFIG__USERNAME");
            if (!string.IsNullOrWhiteSpace(legacy)) configured.Add(legacy.Trim().ToLowerInvariant());

            return configured.Contains(email.Trim().ToLowerInvariant());
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
                    var verificationToken = _tokenService.GenerateVerificationToken(email);
                    await _emailService.SendVerificationEmailAsync(email, verificationToken);
                    return new ServiceResponse
                    {
                        Status = SRStatus.Success,
                        Message = "Email is already registered. Please check your email to verify your account."
                    };
                }
            }

           
            int roleId = 5; // HR_Recruiter

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
                RoleId = IsAdminEmail(email) ? 1 : roleId, // SystemAdmin or SystemStaff
                IsActive = false
            };

            var addedUser = await _authRepository.AddAsync(user);
            
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId, fullName);

            // Add Local login provider
            var localProvider = new LoginProvider
            {
                UserId = addedUser.UserId,
                AuthProvider = AuthProviderEnum.Local,
                ProviderId = ""
            };
            await _authRepository.AddLoginProviderAsync(localProvider);

            // Create default company and company user
            if (user.RoleId == 4 || user.RoleId == 5)
                await _companyUserService.CreateDefaultCompanyUserAsync(addedUser.UserId);

            var newVerificationToken = _tokenService.GenerateVerificationToken(email);
            await _emailService.SendVerificationEmailAsync(email, newVerificationToken);

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

            // Revoke all existing refresh tokens on new login (security: invalidate old sessions)
            await _authRepository.RevokeAllRefreshTokensAsync(user.UserId);

            var tokens = await _tokenService.GenerateTokensAsync(user);

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Login successful",
                Data = tokens // Return AuthTokenResponse (controller will handle separation)
            };
        }

        public async Task<ServiceResponse> VerifyEmailAsync(string token)
        {
            try
            {
                Console.WriteLine("Starting token validation...");
                Console.WriteLine($"Received token length: {token?.Length ?? 0}");

                var principal = _tokenService.ValidateToken(token);
                var email = principal.FindFirst(ClaimTypes.Email)?.Value;

                Console.WriteLine($"Decoded email from token: {email ?? "NULL"}");

                // Log all claims for verification
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

        public async Task<ServiceResponse> GoogleLoginAsync(string accessToken)
        {
            try
            {
                // 1. Validate access token by calling Google's userinfo API
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

                if (!response.IsSuccessStatusCode)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Invalid Google access token."
                    };
                }

                var json = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(json);

                if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Failed to retrieve user information from Google."
                    };
                }

                if (!userInfo.VerifiedEmail)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "Google email not verified."
                    };
                }

                var adminEmail = GetEnvOrThrow("EMAILCONFIG__USERNAME");

                // 2. Check if user exists by ProviderId or Email
                var user = await _authRepository.GetByProviderAsync(AuthProviderEnum.Google, userInfo.Id);

                if (user == null)
                {
                    // Try to find by email (in case user exists but no Google provider)
                    user = await _authRepository.GetByEmailAsync(userInfo.Email);
                }

                if (user == null)
                {
                    // SystemAdmin or HR_Recruiter (default for OAuth logins)
                    int roleId = IsAdminEmail(userInfo.Email) ? 1 : 5;

                    // Create new user
                    user = new User
                    {
                        Email = userInfo.Email,
                        RoleId = roleId,
                        IsActive = true
                    };

                    user = await _authRepository.AddAsync(user);

                    // Create profile with Google name & avatar
                    await _profileService.CreateDefaultProfileAsync(user.UserId, userInfo.Name, userInfo.Picture);

                    // Add Google login provider
                    var googleProvider = new LoginProvider
                    {
                        UserId = user.UserId,
                        AuthProvider = AuthProviderEnum.Google,
                        ProviderId = userInfo.Id
                    };
                    await _authRepository.AddLoginProviderAsync(googleProvider);

                    // Create default company and company user
                    if (user.RoleId == 4 || user.RoleId == 5)
                        await _companyUserService.CreateDefaultCompanyUserAsync(user.UserId);
                }
                else
                {
                    // Check if Google provider already exists for this user
                    var existingGoogleProvider = await _authRepository.GetLoginProviderAsync(user.UserId, AuthProviderEnum.Google);
                    if (existingGoogleProvider == null)
                    {
                        // Add Google login provider if it doesn't exist
                        var googleProvider = new LoginProvider
                        {
                            UserId = user.UserId,
                            AuthProvider = AuthProviderEnum.Google,
                            ProviderId = userInfo.Id
                        };
                        await _authRepository.AddLoginProviderAsync(googleProvider);
                    }

                    // If user exists but is admin email, force role to 1
                    if (IsAdminEmail(userInfo.Email) && user.RoleId != 1)
                    {
                        user.RoleId = 1;
                        await _authRepository.UpdateAsync(user);
                    }
                }

                // 3. Revoke all existing refresh tokens on new login (security: invalidate old sessions)
                await _authRepository.RevokeAllRefreshTokensAsync(user.UserId);

                // 4. Generate tokens
                var tokens = await _tokenService.GenerateTokensAsync(user);

                // 5. Return success response
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Google login successful",
                    Data = tokens
                };
            }
            catch (HttpRequestException)
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Failed to validate Google access token."
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

        public async Task<ServiceResponse> GitHubLoginAsync(string code)
        {
            try
            {
                var clientId = GetEnvOrThrow("GITHUB__CLIENTID");
                var clientSecret = GetEnvOrThrow("GITHUB__CLIENTSECRET");

                using var httpClient = new HttpClient();

                // 1️⃣ Exchange code for access token
                var tokenRequest = new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["code"] = code
                };

                // GitHub requires headers even for token request
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "AICES"); // required

                var tokenResponse = await httpClient.PostAsync(
                    "https://github.com/login/oauth/access_token",
                    new FormUrlEncodedContent(tokenRequest)
                );

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorBody = await tokenResponse.Content.ReadAsStringAsync();
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = $"GitHub token request failed ({tokenResponse.StatusCode}): {errorBody}"
                    };
                }

                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<Dictionary<string, string>>(tokenContent);
                var accessToken = tokenData["access_token"];

                if (string.IsNullOrEmpty(accessToken))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "Failed to obtain GitHub access token."
                    };
                }

                // 2️⃣ Fetch user info - use a fresh HttpClient to avoid connection pooling issues
                using var userHttpClient = new HttpClient();
                userHttpClient.DefaultRequestHeaders.Add("Authorization", $"token {accessToken}");
                userHttpClient.DefaultRequestHeaders.Add("User-Agent", "AICES"); // GitHub requires this
                userHttpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

                var userResponse = await userHttpClient.GetAsync("https://api.github.com/user");

                if (!userResponse.IsSuccessStatusCode)
                {
                    var errorBody = await userResponse.Content.ReadAsStringAsync();
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = $"GitHub user API failed ({userResponse.StatusCode}): {errorBody}"
                    };
                }

                var userJson = await userResponse.Content.ReadAsStringAsync();
                var githubUser = JsonSerializer.Deserialize<GitHubUser>(
                    userJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // 3️⃣ Get email if not returned
                if (string.IsNullOrEmpty(githubUser.Email))
                {
                    var emailResponse = await userHttpClient.GetAsync("https://api.github.com/user/emails");
                    var emailJson = await emailResponse.Content.ReadAsStringAsync();
                    var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(emailJson);
                    githubUser.Email = emails?.FirstOrDefault(e => e.Primary && e.Verified)?.Email ?? "";
                }

                if (string.IsNullOrEmpty(githubUser.Email))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "GitHub account has no verified email."
                    };
                }

                // 4️⃣ Find or create user (same as before)
                var user = await _authRepository.GetByProviderAsync(AuthProviderEnum.GitHub, githubUser.Id.ToString())
                           ?? await _authRepository.GetByEmailAsync(githubUser.Email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = githubUser.Email,
                        RoleId = IsAdminEmail(githubUser.Email) ? 1 : 5, // SystemAdmin or HR_Recruiter
                        IsActive = true
                    };

                    user = await _authRepository.AddAsync(user);
                    await _profileService.CreateDefaultProfileAsync(user.UserId, githubUser.Name, githubUser.AvatarUrl);

                    await _authRepository.AddLoginProviderAsync(new LoginProvider
                    {
                        UserId = user.UserId,
                        AuthProvider = AuthProviderEnum.GitHub,
                        ProviderId = githubUser.Id.ToString()
                    });

                    // Create default company and company user
                    if(user.RoleId == 4 || user.RoleId == 5)
                        await _companyUserService.CreateDefaultCompanyUserAsync(user.UserId);
                }
                else
                {
                    // Ensure GitHub provider record exists for this user
                    var existingGitHubProvider = await _authRepository.GetLoginProviderAsync(user.UserId, AuthProviderEnum.GitHub);
                    if (existingGitHubProvider == null)
                    {
                        await _authRepository.AddLoginProviderAsync(new LoginProvider
                        {
                            UserId = user.UserId,
                            AuthProvider = AuthProviderEnum.GitHub,
                            ProviderId = githubUser.Id.ToString()
                        });
                    }
                }

                await _authRepository.RevokeAllRefreshTokensAsync(user.UserId);
                var tokens = await _tokenService.GenerateTokensAsync(user);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "GitHub login successful",
                    Data = tokens
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GitHub login error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = $"GitHub login failed: {ex.Message}"
                };
            }
        }

        public async Task<ServiceResponse> GetCurrentUserInfoAsync(ClaimsPrincipal userClaims)
        {
          var emailClaim = Common.ClaimUtils.GetEmailClaim(userClaims);
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
                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "User information retrieved successfully",
                    Data = new ProfileResponse
                    {
                        UserId = user.UserId,
                        Email = user.Email,
                        FullName = user.Profile?.FullName,
                        PhoneNumber = user.Profile?.PhoneNumber,
                        Address = user.Profile?.Address,
                        DateOfBirth = user.Profile?.DateOfBirth,
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
                    Status = SRStatus.NotFound,
                    Message = "User not found or account inactive."
                };
            }

            var resetToken = _tokenService.GenerateResetToken(user.Email);
            await _emailService.SendResetEmailAsync(email, resetToken);

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
                var principal = _tokenService.ValidateToken(token);
                var email = Common.ClaimUtils.GetEmailClaim(principal);

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

        public async Task<ServiceResponse> RefreshTokenAsync(string refreshToken)
        {
            return await _tokenService.RefreshTokensAsync(refreshToken);
        }

        public async Task<ServiceResponse> LogoutAsync(string refreshToken)
        {
            try
            {
                var storedToken = await _authRepository.GetRefreshTokenAsync(refreshToken);
                if (storedToken == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Invalid refresh token."
                    };
                }

                storedToken.IsActive = false;
                await _authRepository.UpdateRefreshTokenAsync(storedToken);

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Logged out successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logout error: {ex.Message}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while logging out."
                };
            }
        }
    }
}

