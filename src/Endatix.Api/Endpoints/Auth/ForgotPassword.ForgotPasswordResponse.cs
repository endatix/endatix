namespace Endatix.Api.Endpoints.Auth;

public sealed record ForgotPasswordResponse
{
    public required string Message { get; init; }
}
