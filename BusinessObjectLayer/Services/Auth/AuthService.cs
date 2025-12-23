using BCrypt.Net;
using BusinessObjectLayer.IServices;
using BusinessObjectLayer.IServices.Auth;
using BusinessObjectLayer.Services.Auth.Models;
using Data.Entities;
using Data.Enum;
using Data.Models.Response;
using DataAccessLayer.IRepositories;
using DataAccessLayer.UnitOfWork;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json;

namespace BusinessObjectLayer.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _uow;
        private readonly IProfileService _profileService;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly ICompanyUserService _companyUserService;

        public AuthService(
            IUnitOfWork uow,
            IProfileService profileService,
            ITokenService tokenService,
            IEmailService emailService,
            ICompanyUserService companyUserService)
        {
            _uow = uow;
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

        /// <summary>
        /// Validates user status and returns an error response if validation fails.
        /// Returns null if validation passes.
        /// </summary>
        private ServiceResponse? ValidateUserStatus(User? user)
        {
            if (user == null)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "Account not found." };

            if (!user.IsActive)
                return new ServiceResponse { Status = SRStatus.Unauthorized, Message = "Account is inactive." };

            return user.Status switch
            {
                UserStatusEnum.Unverified => new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Account not verified."
                },
                UserStatusEnum.Locked => new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Account is locked. Please contact admin to unlock."
                },
                _ => null
            };
        }

        public async Task<ServiceResponse> RegisterAsync(string email, string password, string fullName)
        {
            var authRepo = _uow.GetRepository<IAuthRepository>();
            var existedUser = await authRepo.GetByEmailAsync(email);
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

            if (!await authRepo.RoleExistsAsync(roleId))
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
                Status = UserStatusEnum.Unverified,
            };

            var addedUser = await authRepo.AddAsync(user);
            await _uow.SaveChangesAsync();
            
            await _profileService.CreateDefaultProfileAsync(addedUser.UserId, fullName);

            // Add Local login provider
            var localProvider = new LoginProvider
            {
                UserId = addedUser.UserId,
                AuthProvider = AuthProviderEnum.Local,
                ProviderId = ""
            };
            await authRepo.AddLoginProviderAsync(localProvider);
            await _uow.SaveChangesAsync();

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
            var authRepo = _uow.GetRepository<IAuthRepository>();
            var tokenRepo = _uow.GetRepository<ITokenRepository>();
            var user = await authRepo.GetByEmailAsync(email);

            // Validate user status
            var validation = ValidateUserStatus(user);
            if (validation != null)
                return validation;

            // Check password first (for security, don't reveal if user exists)
            if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return new ServiceResponse
                {
                    Status = SRStatus.Unauthorized,
                    Message = "Invalid password"
                };
            }

            // Revoke all existing refresh tokens on new login (security: invalidate old sessions)
            await tokenRepo.RevokeAllRefreshTokensAsync(user.UserId);
            await _uow.SaveChangesAsync();

            var tokens = await _tokenService.GenerateTokensAsync(user);
            await _uow.SaveChangesAsync(); // Save the new refresh token to database

            return new ServiceResponse
            {
                Status = SRStatus.Success,
                Message = "Login successful",
                Data = tokens 
            };
        }

        public async Task<ServiceResponse> VerifyEmailAsync(string token)
        {
            try
            {
                Console.WriteLine("Starting token validation...");
                Console.WriteLine($"Received token length: {token?.Length ?? 0}");

                var principal = _tokenService.ValidateToken(token);
                var email = Common.ClaimUtils.GetEmailClaim(principal);
                // var email = principal.FindFirst(ClaimTypes.Email)?.Value;

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

                var authRepo = _uow.GetRepository<IAuthRepository>();
                var user = await authRepo.GetForUpdateByEmailAsync(email);
                if (user == null)
                    return new ServiceResponse { Status = SRStatus.Error, Message = "User not found." };

                if (user.Status == UserStatusEnum.Verified)
                    return new ServiceResponse { Status = SRStatus.Success, Message = "Email already verified. You can now log in." };

                user.Status = UserStatusEnum.Verified;
                await authRepo.UpdateAsync(user);
                await _uow.SaveChangesAsync();

                Console.WriteLine($"User {user.UserId} Verified successfully.");

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

                var authRepo = _uow.GetRepository<IAuthRepository>();
                var tokenRepo = _uow.GetRepository<ITokenRepository>();
                
                // 2. Check if user exists by ProviderId or Email
                var user = await authRepo.GetByProviderAsync(AuthProviderEnum.Google, userInfo.Id);

                if (user == null)
                {
                    // Try to find by email (in case user exists but no Google provider)
                    user = await authRepo.GetByEmailAsync(userInfo.Email);
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
                        Status = UserStatusEnum.Verified,
                    };

                    user = await authRepo.AddAsync(user);
                    await _uow.SaveChangesAsync();

                    // Create profile with Google name & avatar
                    await _profileService.CreateDefaultProfileAsync(user.UserId, userInfo.Name, userInfo.Picture);

                    // Add Google login provider
                    var googleProvider = new LoginProvider
                    {
                        UserId = user.UserId,
                        AuthProvider = AuthProviderEnum.Google,
                        ProviderId = userInfo.Id
                    };
                    await authRepo.AddLoginProviderAsync(googleProvider);
                    await _uow.SaveChangesAsync();

                    // Create default company and company user
                    if (user.RoleId == 4 || user.RoleId == 5)
                        await _companyUserService.CreateDefaultCompanyUserAsync(user.UserId);
                }
                else
                {
                    // Check if Google provider already exists for this user
                    var existingGoogleProvider = await authRepo.GetLoginProviderAsync(user.UserId, AuthProviderEnum.Google);
                    if (existingGoogleProvider == null)
                    {
                        // Add Google login provider if it doesn't exist
                        var googleProvider = new LoginProvider
                        {
                            UserId = user.UserId,
                            AuthProvider = AuthProviderEnum.Google,
                            ProviderId = userInfo.Id
                        };
                        await authRepo.AddLoginProviderAsync(googleProvider);
                        await _uow.SaveChangesAsync();
                    }

                    // If user exists but is admin email, force role to 1
                    if (IsAdminEmail(userInfo.Email) && user.RoleId != 1)
                    {
                        user.RoleId = 1;
                        await authRepo.UpdateAsync(user);
                        await _uow.SaveChangesAsync();
                    }

                    // Validate user status for existing users
                    var validation = ValidateUserStatus(user);
                    if (validation != null)
                        return validation;
                }

                // 3. Revoke all existing refresh tokens on new login (security: invalidate old sessions)
                await tokenRepo.RevokeAllRefreshTokensAsync(user.UserId);
                await _uow.SaveChangesAsync();

                // 4. Generate tokens
                var tokens = await _tokenService.GenerateTokensAsync(user);
                await _uow.SaveChangesAsync(); // Save the new refresh token to database

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
                    try
                    {
                        var emailResponse = await userHttpClient.GetAsync("https://api.github.com/user/emails");
                        
                        if (emailResponse.IsSuccessStatusCode)
                        {
                            var emailJson = await emailResponse.Content.ReadAsStringAsync();
                            var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(emailJson);
                            
                            // Try primary and verified first, then any verified email
                            githubUser.Email = emails?.FirstOrDefault(e => e.Primary && e.Verified)?.Email
                                            ?? emails?.FirstOrDefault(e => e.Verified)?.Email
                                            ?? "";
                        }
                        else
                        {
                            var errorBody = await emailResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"GitHub email API failed ({emailResponse.StatusCode}): {errorBody}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error fetching GitHub emails: {ex.Message}");
                    }
                }

                if (string.IsNullOrEmpty(githubUser.Email))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Error,
                        Message = "GitHub account has no verified email. Please verify your email address in GitHub settings and try again."
                    };
                }

                var authRepo = _uow.GetRepository<IAuthRepository>();
                var tokenRepo = _uow.GetRepository<ITokenRepository>();
                
                // 4️⃣ Find or create user (same as before)
                var user = await authRepo.GetByProviderAsync(AuthProviderEnum.GitHub, githubUser.Id.ToString())
                           ?? await authRepo.GetByEmailAsync(githubUser.Email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = githubUser.Email,
                        RoleId = IsAdminEmail(githubUser.Email) ? 1 : 5, // SystemAdmin or HR_Recruiter
                        Status = UserStatusEnum.Verified,
                    };

                    user = await authRepo.AddAsync(user);
                    await _uow.SaveChangesAsync();
                    await _profileService.CreateDefaultProfileAsync(user.UserId, githubUser.Name, githubUser.AvatarUrl);

                    await authRepo.AddLoginProviderAsync(new LoginProvider
                    {
                        UserId = user.UserId,
                        AuthProvider = AuthProviderEnum.GitHub,
                        ProviderId = githubUser.Id.ToString()
                    });
                    await _uow.SaveChangesAsync();

                    // Create default company and company user
                    if(user.RoleId == 4 || user.RoleId == 5)
                        await _companyUserService.CreateDefaultCompanyUserAsync(user.UserId);
                }
                else
                {
                    // Ensure GitHub provider record exists for this user
                    var existingGitHubProvider = await authRepo.GetLoginProviderAsync(user.UserId, AuthProviderEnum.GitHub);
                    if (existingGitHubProvider == null)
                    {
                        await authRepo.AddLoginProviderAsync(new LoginProvider
                        {
                            UserId = user.UserId,
                            AuthProvider = AuthProviderEnum.GitHub,
                            ProviderId = githubUser.Id.ToString()
                        });
                        await _uow.SaveChangesAsync();
                    }

                    // Validate user status for existing users
                    var validation = ValidateUserStatus(user);
                    if (validation != null)
                        return validation;
                }

                await tokenRepo.RevokeAllRefreshTokensAsync(user.UserId);
                await _uow.SaveChangesAsync();
                var tokens = await _tokenService.GenerateTokensAsync(user);
                await _uow.SaveChangesAsync(); // Save the new refresh token to database

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
                return new ServiceResponse
                {
                    Status = SRStatus.NotFound,
                    Message = "Email claim not found in token.",
                };
            }

            var authRepo = _uow.GetRepository<IAuthRepository>();
            var user = await authRepo.GetByEmailAsync(emailClaim);

            // Validate user status
            var validation = ValidateUserStatus(user);
            if (validation != null)
                return validation;

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
                        JoinStatus = (user.RoleId == 4 || user.RoleId == 5) ? user.CompanyUser?.JoinStatus.ToString() : null,
                        CompanyName = (user.RoleId == 4 || user.RoleId == 5) && 
                            (user.CompanyUser?.JoinStatus == JoinStatusEnum.Approved) 
                            ? user.CompanyUser?.Company?.Name : null,
                        CompanyStatus = (user.RoleId == 4 || user.RoleId == 5) && 
                            (user.CompanyUser?.JoinStatus == JoinStatusEnum.Approved) 
                            ? user.CompanyUser?.Company?.CompanyStatus.ToString() : null,
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
            var authRepo = _uow.GetRepository<IAuthRepository>();
            var user = await authRepo.GetByEmailAsync(email);
            // Validate user status
            var validation = ValidateUserStatus(user);
            if (validation != null)
                return validation;

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

                var authRepo = _uow.GetRepository<IAuthRepository>();
                var user = await authRepo.GetByEmailAsync(email);
                // Validate user status
                var validation = ValidateUserStatus(user);
                if (validation != null)
                    return validation;

                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await authRepo.UpdateAsync(user);
                await _uow.SaveChangesAsync();

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
                var tokenRepo = _uow.GetRepository<ITokenRepository>();
				var authRepo = _uow.GetRepository<IAuthRepository>();
                var storedToken = await tokenRepo.GetRefreshTokenForUpdateAsync(refreshToken);
                if (storedToken == null)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.NotFound,
                        Message = "Invalid refresh token."
                    };
                }

                storedToken.IsActive = false;
                await tokenRepo.UpdateRefreshTokenAsync(storedToken);

				// Clear current session if this refresh token belongs to the active session user
				var user = storedToken.User;
				if (user != null)
				{
					var userForUpdate = await authRepo.GetForUpdateByIdAsync(user.UserId);
					if (userForUpdate != null)
					{
						userForUpdate.CurrentSessionId = null;
						userForUpdate.CurrentSessionExpiry = null;
						await authRepo.UpdateAsync(userForUpdate);
					}
				}

                await _uow.SaveChangesAsync();

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

        public async Task<ServiceResponse> ChangePasswordAsync(ClaimsPrincipal userClaims, string oldPassword, string newPassword)
        {
            try
            {
                var authRepo = _uow.GetRepository<IAuthRepository>();
                
                // Get user ID from claims
                var userIdClaim = Common.ClaimUtils.GetUserIdClaim(userClaims);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not authenticated."
                    };
                }

                int userId = int.Parse(userIdClaim);

                // Get user from database
                var user = await authRepo.GetByEmailAsync(Common.ClaimUtils.GetEmailClaim(userClaims));
                if (user == null || user.UserId != userId)
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Unauthorized,
                        Message = "User not found."
                    };
                }

                // Validate user status
                var validation = ValidateUserStatus(user);
                if (validation != null)
                    return validation;

                // Verify old password
                if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.Password))
                {
                    return new ServiceResponse
                    {
                        Status = SRStatus.Validation,
                        Message = "Old password is incorrect."
                    };
                }

                // Hash and update new password
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await authRepo.UpdateAsync(user);
                await _uow.SaveChangesAsync();

                return new ServiceResponse
                {
                    Status = SRStatus.Success,
                    Message = "Password changed successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Change password error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new ServiceResponse
                {
                    Status = SRStatus.Error,
                    Message = "An error occurred while changing the password."
                };
            }
        }
    }
}

