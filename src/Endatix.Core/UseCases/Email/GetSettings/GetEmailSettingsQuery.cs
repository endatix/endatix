using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Email.GetSettings;

/// <summary>
/// Query to get email settings.
/// </summary>
/// <returns>The result.</returns>
public record GetEmailSettingsQuery() : IQuery<Result<EmailSettingsDto>>;
