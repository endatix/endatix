namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// A form's current state, as a form access token is checked against it on every use.
/// </summary>
/// <param name="FormId">The form.</param>
/// <param name="TenantId">The form's tenant.</param>
/// <param name="IsPublic">Whether anyone may open the form.</param>
/// <param name="IsEnabled">Whether the form accepts respondents.</param>
public sealed record FormRuntimeScope(long FormId, long TenantId, bool IsPublic, bool IsEnabled);
