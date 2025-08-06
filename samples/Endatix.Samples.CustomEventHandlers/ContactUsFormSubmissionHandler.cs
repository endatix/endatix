using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Endatix.Core.Features.Email;
using Endatix.Core.Events;
using Endatix.Core;

namespace Endatix.Samples.CustomEventHandlers;

public class ContactUsFormSubmissionHandler : INotificationHandler<SubmissionCompletedEvent>
{
    private readonly IEmailSender _emailSender;

    private readonly ILogger<ContactUsFormSubmissionHandler> _logger;

    private readonly ContactUsOptions _settings;

    public ContactUsFormSubmissionHandler(IEmailSender emailSender, ILogger<ContactUsFormSubmissionHandler> logger, IOptions<ContactUsOptions> options)
    {
        _emailSender = emailSender;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task Handle(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling form submission via the {handler}", nameof(ContactUsFormSubmissionHandler));

        await SendWelcomeEmailBackToSenderAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        await SendSystemNotificationEmailAsync(domainEvent, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendWelcomeEmailBackToSenderAsync(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        var submissionData = EmailHelper.ParseJsonToDictionary(domainEvent.Submission.JsonData);
        if (submissionData.TryGetValue("email", out var email))
        {
            var emailModel = new EmailWithTemplate()
            {
                To = email,
                TemplateId = _settings.ContactUsResponseTemplateId
            };

            var name = submissionData["name"] ?? string.Empty;
            if (!string.IsNullOrEmpty(name))
            {
                _ = emailModel.MetadataOperations.AddMetadataValue(nameof(name), name);
            }

            var validationResult = new EmailWithTemplateValidator().Validate(emailModel);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Cannot system notification email as part of {operation} due to invalid data: {errors}", "Send Welcome Email", validationResult.ToString("~"));
                return;
            }
            await _emailSender.SendEmailAsync(emailModel, cancellationToken);
        }
    }

    private async Task SendSystemNotificationEmailAsync(SubmissionCompletedEvent domainEvent, CancellationToken cancellationToken)
    {
        var subject = $"New Contact Us Form Submission";
        var formattedSubmission = EmailHelper.ToFormattedMessage(domainEvent.Submission);
        var htmlBody = $"Submission was completed with Id: @{domainEvent.Submission.Id} was completed. <br><h2>Details:</h2> {formattedSubmission}";

        var emailModel = new EmailWithBody()
        {
            To = _settings.NotificationEmailTo,
            From = _settings.NotificationEmailFrom,
            Subject = subject,
            HtmlBody = htmlBody,
            PlainTextBody = formattedSubmission
        };

        var validationResult = new EmailWithBodyValidator().Validate(emailModel);
        if (!validationResult.IsValid)
        {
            _logger.LogError("Cannot system notification email as part of {operation} due to invalid data: {errors}", "Send System Notification Email", validationResult.ToString("~"));
            return;
        }
        await _emailSender.SendEmailAsync(emailModel, cancellationToken);
    }
}