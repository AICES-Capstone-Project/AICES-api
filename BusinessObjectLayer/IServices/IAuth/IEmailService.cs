namespace BusinessObjectLayer.Services.Auth
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken);
        Task SendResetEmailAsync(string email, string resetToken);
    }
}

