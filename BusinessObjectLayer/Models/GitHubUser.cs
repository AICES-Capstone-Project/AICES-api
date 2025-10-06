namespace BusinessObjectLayer.Services.Auth.Models
{
    public class GitHubUser
    {
        public int Id { get; set; }
        public string Login { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string AvatarUrl { get; set; } = string.Empty;
    }
}

