using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BusinessObjectLayer.Services.Auth
{
    public class EmailService : IEmailService
    {
        private static string GetEnvOrThrow(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing environment variable: {key}");
            }
            return value;
        }

        public async Task SendVerificationEmailAsync(string email, string verificationToken)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", Environment.GetEnvironmentVariable("EMAILCONFIG__FROM")));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Verify Your Email";

            var builder = new BodyBuilder();
            // URL encode the token to prevent issues with special characters
            var encodedToken = System.Web.HttpUtility.UrlEncode(verificationToken);
            var verificationLink = $"{Environment.GetEnvironmentVariable("APPURL__CLIENTURL")}/verify-email?token={encodedToken}";
            builder.HtmlBody = $@"
                <h1>Verify Your Account</h1>
                <p>Please click the link below to verify your email:</p>
                <a href='{verificationLink}'>Verify Email</a>
                <p><small>This link will expire in 15 minutes.</small></p>";

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                Environment.GetEnvironmentVariable("EMAILCONFIG__SMTPSERVER"),
                int.Parse(Environment.GetEnvironmentVariable("EMAILCONFIG__SMTPPORT") ?? "587"),
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(
                Environment.GetEnvironmentVariable("EMAILCONFIG__USERNAME"),
                Environment.GetEnvironmentVariable("EMAILCONFIG__PASSWORD"));
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendResetEmailAsync(string email, string resetToken)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", GetEnvOrThrow("EMAILCONFIG__FROM")));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Reset Your Password";

            var builder = new BodyBuilder();
            var encodedToken = System.Web.HttpUtility.UrlEncode(resetToken);
            var resetLink = $"{GetEnvOrThrow("APPURL__CLIENTURL")}/reset-password?token={encodedToken}";
            builder.HtmlBody = $"<h1>Reset Your Password</h1><p>Please click the link below to reset your password:</p><a href='{resetLink}'>{resetLink}</a><p>This link will expire in 15 minutes.</p>";
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                GetEnvOrThrow("EMAILCONFIG__SMTPSERVER"),
                int.Parse(GetEnvOrThrow("EMAILCONFIG__SMTPPORT")),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                GetEnvOrThrow("EMAILCONFIG__USERNAME"),
                GetEnvOrThrow("EMAILCONFIG__PASSWORD"));
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendReceiptEmailAsync(string email, string invoiceUrl, decimal amount, string currency, string subscriptionName, string receiptNumber = null, string paymentMethod = null, DateTime? datePaid = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", GetEnvOrThrow("EMAILCONFIG__FROM")));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Receipt from AICES";

            var builder = new BodyBuilder();
            var amountFormatted = $"{currency.ToUpper()} {amount:F2}";
            builder.HtmlBody = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                    <h1 style=""color: #118f00;"">Receipt from AICES</h1>
                    <p>Thank you for your payment!</p>
                    <div style=""background-color: #f5f5f5; padding: 20px; border-radius: 5px; margin: 20px 0;"">
                        <h2>Payment Details</h2>
                        <p><strong>Subscription:</strong> {subscriptionName}</p>
                        <p><strong>Amount Paid:</strong> {amountFormatted}</p>
                        <p><strong>Date:</strong> {DateTime.UtcNow:MMMM dd, yyyy}</p>
                    </div>
                    <p>You can view and download your invoice by clicking the link below:</p>
                    <p><a href=""{invoiceUrl}"" style=""background-color: #118f00; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;"">View Invoice</a></p>
                    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">If you have any questions, please contact our support team.</p>
                </div>";

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                GetEnvOrThrow("EMAILCONFIG__SMTPSERVER"),
                int.Parse(GetEnvOrThrow("EMAILCONFIG__SMTPPORT")),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                GetEnvOrThrow("EMAILCONFIG__USERNAME"),
                GetEnvOrThrow("EMAILCONFIG__PASSWORD"));
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendCompanyApprovalEmailAsync(string email, string companyName)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("AICES", GetEnvOrThrow("EMAILCONFIG__FROM")));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = "Công ty của bạn đã được phê duyệt - Your Company Has Been Approved";

            var builder = new BodyBuilder();
            builder.HtmlBody = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;"">
                    <h1 style=""color: #118f00;"">Chúc mừng! Công ty của bạn đã được phê duyệt</h1>
                    <p>Xin chào,</p>
                    <p>Chúng tôi rất vui mừng thông báo rằng công ty <strong>{companyName}</strong> của bạn đã được phê duyệt thành công.</p>
                    <div style=""background-color: #f5f5f5; padding: 20px; border-radius: 5px; margin: 20px 0;"">
                        <h2>Thông tin công ty</h2>
                        <p><strong>Tên công ty:</strong> {companyName}</p>
                        <p><strong>Trạng thái:</strong> Đã được phê duyệt</p>
                        <p><strong>Ngày phê duyệt:</strong> {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}</p>
                    </div>
                    <p>Bây giờ bạn có thể bắt đầu sử dụng các tính năng của hệ thống AICES để quản lý công ty và đăng tin tuyển dụng.</p>
                    <p>Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với đội ngũ hỗ trợ của chúng tôi.</p>
                    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">Trân trọng,<br>Đội ngũ AICES</p>
                    <hr style=""margin: 30px 0; border: none; border-top: 1px solid #ddd;"">
                    <h1 style=""color: #118f00;"">Congratulations! Your Company Has Been Approved</h1>
                    <p>Hello,</p>
                    <p>We are pleased to inform you that your company <strong>{companyName}</strong> has been successfully approved.</p>
                    <div style=""background-color: #f5f5f5; padding: 20px; border-radius: 5px; margin: 20px 0;"">
                        <h2>Company Information</h2>
                        <p><strong>Company Name:</strong> {companyName}</p>
                        <p><strong>Status:</strong> Approved</p>
                        <p><strong>Approval Date:</strong> {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}</p>
                    </div>
                    <p>You can now start using AICES system features to manage your company and post job listings.</p>
                    <p>If you have any questions, please contact our support team.</p>
                    <p style=""color: #666; font-size: 12px; margin-top: 30px;"">Best regards,<br>AICES Team</p>
                </div>";

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                GetEnvOrThrow("EMAILCONFIG__SMTPSERVER"),
                int.Parse(GetEnvOrThrow("EMAILCONFIG__SMTPPORT")),
                SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                GetEnvOrThrow("EMAILCONFIG__USERNAME"),
                GetEnvOrThrow("EMAILCONFIG__PASSWORD"));
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}

