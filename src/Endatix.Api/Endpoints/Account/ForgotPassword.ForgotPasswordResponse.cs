namespace Endatix.Api.Endpoints.Account;

public sealed record ForgotPasswordResponse
{
    public required string Message { get; init; }
}
