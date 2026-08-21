using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.UseCases.Identity.ExitAssume;

/// <summary>
/// Returns the current PlatformAdmin to their home tenant and drops the <c>act</c> claim.
/// </summary>
public sealed record ExitAssumeCommand : ICommand<Result<AuthTokensDto>>;
