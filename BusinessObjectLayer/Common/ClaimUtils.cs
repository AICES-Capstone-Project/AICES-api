using System.Security.Claims;

namespace BusinessObjectLayer.Common
{
    public static class ClaimUtils
{
    private static readonly string[] UserIdClaimKeys =
    {
        ClaimTypes.NameIdentifier,        
        "nameidentifier",                 
        "sub",                            
        "id",                             
        "userid",                         
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
    };

    private static readonly string[] EmailClaimKeys =
    {
        ClaimTypes.Email,     
        "email",
        "mail"
    };

    public static string? GetUserIdClaim(ClaimsPrincipal userClaims)
    {
        foreach (var key in UserIdClaimKeys)
        {
            var claim = userClaims.FindFirst(key)?.Value;
            if (!string.IsNullOrEmpty(claim))
                return claim;
        }
        return null;
    }

    public static string? GetEmailClaim(ClaimsPrincipal userClaims)
    {
        foreach (var key in EmailClaimKeys)
        {
            var claim = userClaims.FindFirst(key)?.Value;
            if (!string.IsNullOrEmpty(claim))
                return claim;
        }
        return null;
    }
}

}
