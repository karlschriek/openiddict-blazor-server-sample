using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Blazor.Server.Data;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace OpenIddict.Blazor.Server.Components.Account;

public class SendGridEmailSender(IOptions<SendGridEmailSenderOptions> optionsAccessor,
    ILogger<SendGridEmailSender> logger) : IEmailSender<ApplicationUser>
{
    private readonly ILogger logger = logger;

    public SendGridEmailSenderOptions Options { get; } = optionsAccessor.Value;

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, 
        string confirmationLink) => SendEmailAsync(email, "Confirm your email", 
        $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, 
        string resetLink) => SendEmailAsync(email, "Reset your password", 
        $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, 
        string resetCode) => SendEmailAsync(email, "Reset your password", 
        $"Please reset your password using the following code: {resetCode}");

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        if (string.IsNullOrEmpty(Options.ApiKey))
        {
            throw new Exception("Null EmailAuthKey");
        }

        await Execute(Options.ApiKey, subject, message, toEmail);
    }

    public async Task Execute(string apiKey, string subject, string message, string toEmail)
    {
        var client = new SendGridClient(apiKey);
        var msg = new SendGridMessage()
        {
            From = new EmailAddress(Options.FromEmail, Options.FromName),
            Subject = subject,
            PlainTextContent = message,
            HtmlContent = message
        };
        msg.AddTo(new EmailAddress(toEmail));

        // Disable click tracking.
        // See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
        msg.SetClickTracking(false, false);
        var response = await client.SendEmailAsync(msg);
        
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Email to {Email} queued successfully!", toEmail);
        }
        else
        {
            logger.LogInformation("Failure Email to {Email}", toEmail);
        }
    }
}