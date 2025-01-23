namespace Endatix.Api.Endpoints.Account;

/// <summary>
/// Response model for the password change operation
/// </summary>
/// <param name="Message">The message indicating the result of the operation</param>
public record ChangePasswordResponse(string Message)
{
    /// <summary>
    /// Gets the message included in the change password response
    /// </summary>
    public string Message { get; init; } = Message;
}
