using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.UseCases.Identity.SwitchTenant;

/// <summary>
/// Remints the session for a tenant the current user already belongs to. Updates last-used <c>AppUser.TenantId</c>.
/// </summary>
public sealed record SwitchTenantCommand(long TenantId) : ICommand<Result<AuthTokensDto>>;
