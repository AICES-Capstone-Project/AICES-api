namespace BusinessObjectLayer.Services.Auth
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationToken);
        Task SendResetEmailAsync(string email, string resetToken);
        Task SendReceiptEmailAsync(string email, string invoiceUrl, decimal amount, string currency, string subscriptionName, string receiptNumber = null, string paymentMethod = null, DateTime? datePaid = null);
        Task SendCompanyApprovalEmailAsync(string email, string companyName);
    }
}

