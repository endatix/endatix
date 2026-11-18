using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.TenantBranding.Rollback;

/// <summary>
/// Returns the current tenant to its previous brand override.
/// </summary>
/// <param name="ExpectedRevision">The revision the client read, so a retried rollback is refused instead of undoing itself.</param>
public sealed record RollbackTenantBrandingCommand(long ExpectedRevision) : ICommand<Result<TenantBrandingDto>>;
