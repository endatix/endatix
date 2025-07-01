using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Ardalis.GuardClauses;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// SMTP email sender implementation using .NET's SmtpClient.
/// Supports both direct HTML/Plain text emails and database template rendering.
/// </summary>
public class SmtpEmailSender : IEmailSender, IHasConfigSection<SmtpSettings>, IPluginInitializer
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly SmtpSettings _settings;
    private readonly IRepository<EmailTemplate> _templateRepository;

    public SmtpEmailSender(
        ILogger<SmtpEmailSender> logger, 
        IOptions<SmtpSettings> options,
        IRepository<EmailTemplate> templateRepository)
    {
        _logger = logger;
        _settings = options.Value;
        _templateRepository = templateRepository;
        
        Guard.Against.Null(_settings);
        Guard.Against.NullOrEmpty(_settings.Host);
        Guard.Against.NullOrEmpty(_settings.DefaultFromAddress);
    }

    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        // No additional services needed for SMTP
        // SmtpClient is created per email for thread safety
    };

    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrEmpty(email.To);
        Guard.Against.NullOrEmpty(email.Subject);

        using var smtpClient = CreateSmtpClient();
        using var mailMessage = CreateMailMessage(email);

        await smtpClient.SendMailAsync(mailMessage, cancellationToken);

        _logger.LogInformation("SMTP email sent successfully to {To} with subject {Subject}", 
            email.To, email.Subject);
    }

    public async Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrEmpty(email.To);
        Guard.Against.NullOrEmpty(email.TemplateId);

        var template = await _templateRepository.FirstOrDefaultAsync(new EmailTemplateByNameSpec(email.TemplateId));
        if (template == null)
        {
            throw new InvalidOperationException($"Email template '{email.TemplateId}' not found in database");
        }

        // Convert metadata to string dictionary for template rendering
        var variables = email.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);

        var emailWithBody = template.Render(
            email.To,
            variables, 
            subject: email.Subject, 
            from: email.From);
        
        await SendEmailAsync(emailWithBody, cancellationToken);
    }

    private SmtpClient CreateSmtpClient()
    {
        var smtpClient = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = string.IsNullOrEmpty(_settings.Username)
        };

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            smtpClient.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        return smtpClient;
    }

    private MailMessage CreateMailMessage(EmailWithBody email)
    {
        var fromAddress = string.IsNullOrEmpty(email.From) ? _settings.DefaultFromAddress : email.From;
        var fromDisplayName = _settings.DefaultFromName;

        var mailMessage = new MailMessage
        {
            From = new MailAddress(fromAddress, fromDisplayName),
            Subject = email.Subject,
            Body = email.HtmlBody ?? email.PlainTextBody ?? string.Empty,
            IsBodyHtml = !string.IsNullOrEmpty(email.HtmlBody)
        };

        mailMessage.To.Add(email.To);

        // Add plain text alternative if both HTML and plain text are provided
        if (!string.IsNullOrEmpty(email.HtmlBody) && !string.IsNullOrEmpty(email.PlainTextBody))
        {
            var plainTextView = AlternateView.CreateAlternateViewFromString(
                email.PlainTextBody, 
                System.Text.Encoding.UTF8, 
                "text/plain");
            mailMessage.AlternateViews.Add(plainTextView);
        }

        return mailMessage;
    }
} 