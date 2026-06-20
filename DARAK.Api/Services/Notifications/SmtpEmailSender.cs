using System.Net;
using System.Net.Mail;
using DARAK.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace DARAK.Api.Services.Notifications;

public sealed class SmtpEmailSender(IOptions<NotificationOptions> options) : IEmailSender
{
    public async Task<NotificationDeliveryResult> SendAsync(
        EmailNotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var emailOptions = options.Value.Email;
        if (!emailOptions.Enabled)
        {
            return NotificationDeliveryResult.Skipped(emailOptions.ProviderName, "SMTP email delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(emailOptions.Host)
            || string.IsNullOrWhiteSpace(emailOptions.FromEmail)
            || string.IsNullOrWhiteSpace(message.ToEmail))
        {
            return NotificationDeliveryResult.Skipped(
                emailOptions.ProviderName,
                "SMTP email delivery is not fully configured.");
        }

        try
        {
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(emailOptions.FromEmail, emailOptions.FromName),
                Subject = message.Subject,
                Body = message.Body,
                IsBodyHtml = false
            };

            mailMessage.To.Add(new MailAddress(message.ToEmail, message.ToName));

            using var smtpClient = new SmtpClient(emailOptions.Host, emailOptions.Port)
            {
                EnableSsl = emailOptions.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(emailOptions.Username))
            {
                smtpClient.Credentials = new NetworkCredential(
                    emailOptions.Username,
                    emailOptions.Password);
            }

            await smtpClient.SendMailAsync(mailMessage).WaitAsync(cancellationToken);
            return NotificationDeliveryResult.Success(emailOptions.ProviderName);
        }
        catch (Exception exception)
        {
            return NotificationDeliveryResult.Failed(emailOptions.ProviderName, exception.Message);
        }
    }
}
