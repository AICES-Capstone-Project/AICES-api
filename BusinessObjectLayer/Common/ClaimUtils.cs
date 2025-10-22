using System.Security.Claims;

namespace BusinessObjectLayer.Common
{
    public static class ClaimUtils
    {
        public static string? GetEmailClaim(ClaimsPrincipal userClaims)
        {
            return userClaims.FindFirst(ClaimTypes.Email)?.Value
                ?? userClaims.FindFirst("email")?.Value;
        }

        public static string? GetUserIdClaim(ClaimsPrincipal userClaims)
        {
            return userClaims.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
