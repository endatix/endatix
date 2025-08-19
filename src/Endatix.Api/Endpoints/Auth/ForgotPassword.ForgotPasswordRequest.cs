namespace Endatix.Api.Endpoints.Auth;

public sealed record ForgotPasswordRequest
{
    public required string Email { get; init; }
}
