using Endatix.Core.Features.Auth;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Admin.Auth.GetSettings;

/// <summary>
/// Handler for the GetAuthSettingsQuery.
/// </summary>
public sealed class GetAuthSettingsHandler(IAuthSettingsReader authSettingsReader)
    : IQueryHandler<GetAuthSettingsQuery, Result<AuthSettingsDto>>
{
    /// <inheritdoc />
    public Task<Result<AuthSettingsDto>> Handle(
        GetAuthSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = authSettingsReader.GetSettings();
        return Task.FromResult(Result.Success(settings));
    }
}
