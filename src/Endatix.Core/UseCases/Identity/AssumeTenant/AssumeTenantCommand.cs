using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.UseCases.Identity.AssumeTenant;

/// <summary>
/// Issues a new session in a target tenant for the current PlatformAdmin. Does not create membership.
/// </summary>
public sealed record AssumeTenantCommand(long TenantId) : ICommand<Result<AuthTokensDto>>;
