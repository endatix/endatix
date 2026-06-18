using Endatix.Core.Features.Auth;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Admin.Auth.GetSettings;

/// <summary>
/// Query to get platform auth settings.
/// </summary>
public record GetAuthSettingsQuery() : IQuery<Result<AuthSettingsDto>>;
