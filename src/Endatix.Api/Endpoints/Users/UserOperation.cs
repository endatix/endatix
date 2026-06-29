namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Generic response for user mutation operations.
/// </summary>
public sealed record UserOperation
{

    /// <summary>
    /// Initializes a new instance of the <see cref="UserOperation"/> class.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the operation succeeded.</param>
    /// <param name="message">A message describing the result.</param>
    private UserOperation(bool isSuccess, string message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// A message describing the result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates a new instance of the <see cref="UserOperation"/> class with a success result.
    /// </summary>
    /// <param name="message">A message describing the result.</param>
    /// <returns>A new instance of the <see cref="UserOperation"/> class.</returns>
    public static UserOperation Success(string message = "Operation completed successfully.")
    {
        return new UserOperation(true, message);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="UserOperation"/> class with a failure result.
    /// </summary>
    /// <param name="message">A message describing the result.</param>
    /// <returns>A new instance of the <see cref="UserOperation"/> class.</returns>
    public static UserOperation Failure(string message = "Operation failed.")
    {
        return new UserOperation(false, message);
    }
}
