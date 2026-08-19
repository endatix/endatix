using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core;
using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Infrastructure.Email;

/// <summary>
/// SMTP email sender implementation using MailKit's SmtpClient (https://github.com/jstedfast/MailKit).
/// Supports both direct HTML/Plain text emails and database template rendering.
/// </summary>
public class SmtpEmailSender : IEmailSender, IHasConfigSection<SmtpSettings>, IPluginInitializer
{
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly SmtpSettings _settings;
    private readonly EmailTemplateRenderer _templateRenderer;

    // Static (not per-instance): IEmailSender is registered scoped, so an instance field would
    // re-warn on every request on an anonymous-relay deployment instead of once for the process.
    private static int _blankUsernameWarningLogged;

    /// <summary>
    /// Initializes a new instance of the SmtpEmailSender class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The SMTP settings.</param>
    /// <param name="templateRenderer">The email template renderer.</param>
    public SmtpEmailSender(
        ILogger<SmtpEmailSender> logger,
        IOptions<SmtpSettings> options,
        EmailTemplateRenderer templateRenderer)
    {
        _logger = logger;
        _settings = options.Value;
        _templateRenderer = templateRenderer;

        Guard.Against.Null(_settings);
        Guard.Against.NullOrEmpty(_settings.Host);
        Guard.Against.NullOrEmpty(_settings.DefaultFromAddress);
    }

    public static Action<IServiceCollection> InitializationDelegate => (services) =>
    {
        // No additional services needed for SMTP
        // A transport is created per email for thread safety
    };

    /// <inheritdoc />
    public string ProviderName => "SMTP";

    /// <inheritdoc />
    public bool IsConfigured => !string.Equals(_settings.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    public async Task SendEmailAsync(EmailWithBody email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);
        Guard.Against.NullOrEmpty(email.To);
        Guard.Against.NullOrEmpty(email.Subject);

        using var smtpClient = CreateTransport();
        using var mimeMessage = CreateMimeMessage(email);

        Exception? primaryException = null;
        try
        {
            await ConnectAndAuthenticateAsync(smtpClient, cancellationToken);
            await smtpClient.SendAsync(mimeMessage, cancellationToken);

            // Logged here, not after the finally: a DisconnectAsync failure during cleanup must not
            // suppress this record of a send that already succeeded.
            _logger.LogInformation("SMTP email sent successfully to {To} with subject {Subject}",
                SensitiveValue.Email(email.To), email.Subject);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            try
            {
                // CancellationToken.None: an already-cancelled token here would make the cleanup
                // itself throw, masking whatever exception is already propagating from the try block.
                if (smtpClient.IsConnected)
                {
                    await smtpClient.DisconnectAsync(true, CancellationToken.None);
                }
            }
            catch (Exception cleanupException) when (primaryException is not null)
            {
                // A send/connect failure is already in flight and more diagnostically useful than a
                // cleanup failure — log the latter instead of letting it replace the former.
                _logger.LogWarning(cleanupException, "SMTP disconnect failed after an earlier SMTP failure.");
            }
        }
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(EmailWithTemplate email, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(email);

        var emailWithBody = await _templateRenderer.RenderAsync(email, cancellationToken);

        await SendEmailAsync(emailWithBody, cancellationToken);
    }

    /// <summary>
    /// Creates the mail transport used to connect and send. A protected seam so tests can substitute
    /// a fake transport instead of talking to a real SMTP server.
    /// </summary>
    protected virtual IMailTransport CreateTransport() => new SmtpClient();

    private async Task ConnectAndAuthenticateAsync(IMailTransport smtpClient, CancellationToken cancellationToken)
    {
        smtpClient.CheckCertificateRevocation = _settings.CheckCertificateRevocation;

        await smtpClient.ConnectAsync(_settings.Host, _settings.Port, ResolveSecureSocketOptions(), cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
        {
            await smtpClient.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        }
        else if (Interlocked.Exchange(ref _blankUsernameWarningLogged, 1) == 0)
        {
            _logger.LogWarning(
                "SMTP username is not configured; connecting to {Host} without authentication. " +
                "Default Windows credentials are no longer used for anonymous SMTP.",
                _settings.Host);
        }
    }

    /// <summary>
    /// Resets the process-wide blank-username warning gate. Test-only: without this, whichever test
    /// happens to run first would consume the one-time warning for the rest of the test process.
    /// </summary>
    internal static void ResetBlankUsernameWarningForTests() => Interlocked.Exchange(ref _blankUsernameWarningLogged, 0);

    /// <summary>
    /// Maps <see cref="SmtpSettings"/> to a MailKit <see cref="SecureSocketOptions"/>. When
    /// <see cref="SmtpSettings.SecurityMode"/> is <see cref="SmtpSecurityMode.Auto"/> (the default),
    /// this reproduces the behavior of the previous System.Net.Mail-based sender — which only ever
    /// spoke STARTTLS — while additionally recognizing port 465 as implicit TLS. StartTls (not Auto)
    /// is used for the derived STARTTLS case because MailKit's Auto option silently falls back to an
    /// unencrypted connection when the server doesn't advertise STARTTLS, whereas EnableSsl = true
    /// previously guaranteed encryption or failure.
    /// </summary>
    internal SecureSocketOptions ResolveSecureSocketOptions() => _settings.SecurityMode switch
    {
        SmtpSecurityMode.None => SecureSocketOptions.None,
        SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurityMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
        SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => !_settings.EnableSsl
            ? SecureSocketOptions.None
            : _settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls
    };

    private MimeMessage CreateMimeMessage(EmailWithBody email)
    {
        var fromAddress = string.IsNullOrEmpty(email.From) ? _settings.DefaultFromAddress : email.From;

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_settings.DefaultFromName, fromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(email.To));
        mimeMessage.Subject = email.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = email.HtmlBody,
            TextBody = email.PlainTextBody
        };

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        return mimeMessage;
    }
}
