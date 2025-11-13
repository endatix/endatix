namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Interface for mapping external authorization data to Endatix AuthorizationData details.
/// </summary>
public interface IExternalAuthorizationMapper
{
    /// <summary>
    /// Maps the external roles to the Endatix AppRoles.
    /// </summary>
    /// <param name="externalRoles">The external roles to map.</param>
    /// <param name="roleMappings">The role mappings to use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Endatix AppRoles.</returns>
    Task<MappingResult> MapToAppRolesAsync(string[] externalRoles, Dictionary<string, string> roleMappings, CancellationToken cancellationToken);


    /// <summary>
    /// Result of the mapping process.
    /// </summary>
    record MappingResult
    {
        public bool IsSuccess { get; init; }
        public string[] Roles { get; init; }
        public string[] Permissions { get; init; }
        public string? ErrorMessage { get; init; }

        private MappingResult(bool isSuccess, string[] roles, string[] permissions, string? errorMessage = null)
        {
            IsSuccess = isSuccess;
            Roles = roles;
            Permissions = permissions;
            ErrorMessage = errorMessage;
        }

        public static MappingResult Success(string[] roles, string[] permissions) => new(true, roles, permissions);
        public static MappingResult Empty() => new(true, [], []);
        public static MappingResult Failure(string errorMessage) => new(false, [], [], errorMessage);
    }
}