using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Email.GetSettings;

/// <summary>
/// Handler for the GetEmailSettingsQuery.
/// </summary>
/// <param name="emailSender">The email sender.</param>
public class GetEmailSettingsHandler(
    IEmailSender emailSender
) : IQueryHandler<GetEmailSettingsQuery, Result<EmailSettingsDto>>
{
    /// <summary>
    /// Handles the GetEmailSettingsQuery.
    /// </summary>
    /// <param name="request">The GetEmailSettingsQuery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public Task<Result<EmailSettingsDto>> Handle(GetEmailSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = new EmailSettingsDto(emailSender.ProviderName, emailSender.IsConfigured);
        return Task.FromResult(Result.Success(settings));
    }
}
