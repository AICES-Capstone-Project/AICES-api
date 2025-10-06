namespace BusinessObjectLayer.Services.Auth.Models
{
    public class GitHubEmail
    {
        public string Email { get; set; } = string.Empty;
        public bool Primary { get; set; }
        public bool Verified { get; set; }
    }
}

