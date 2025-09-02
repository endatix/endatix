namespace Endatix.Api.Endpoints.Account;

public sealed record ForgotPasswordRequest
{
    public required string Email { get; init; }
}
