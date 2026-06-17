namespace Endatix.Api.Endpoints.Admin.PlatformAdmins;

/// <summary>
/// Generic response for platform administrator mutation operations.
/// </summary>
public sealed record PlatformAdminOperation(bool IsSuccess, string Message)
{
    public static PlatformAdminOperation Success(string message) => new(true, message);
}
