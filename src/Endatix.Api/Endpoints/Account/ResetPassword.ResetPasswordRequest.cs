using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Api.Endpoints.Account;

public sealed record ResetPasswordRequest
{
    public required string Email { get; init; }

    [Sensitive]
    public required string ResetCode { get; init; }

    [Sensitive]
    public required string NewPassword { get; init; }

    [Sensitive]
    public required string ConfirmPassword { get; init; }
}
