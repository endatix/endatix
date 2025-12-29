using Endatix.Core.Infrastructure.Logging;

namespace Endatix.Api.Endpoints.Account;

public sealed record ResetPasswordRequest
{
    [Sensitive(SensitivityType.Email)]
    public required string Email { get; init; }

    [Sensitive(SensitivityType.Secret)]
    public required string ResetCode { get; init; }

    [Sensitive(SensitivityType.Secret)]
    public required string NewPassword { get; init; }

    [Sensitive(SensitivityType.Secret)]
    public required string ConfirmPassword { get; init; }
}
