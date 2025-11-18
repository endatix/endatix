using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantSettings.Get;

/// <summary>
/// Query for retrieving tenant settings for the current tenant.
/// The tenant ID is resolved from the current request context.
/// </summary>
public record GetTenantSettingsQuery : IQuery<Result<TenantSettingsDto>>;
