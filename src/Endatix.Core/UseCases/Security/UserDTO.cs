namespace Endatix.Core.UseCases.Security;

/// <summary>
/// Basic User class DTO to handle transfer of user data between application components
/// </summary>
public record UserDto(string Email, string[] Roles, string SystemInfo);
