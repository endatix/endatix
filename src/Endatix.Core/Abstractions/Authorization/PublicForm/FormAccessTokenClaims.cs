namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Claims carried by a minimal form-scoped ReBAC JWT (<c>frm</c> + <c>tid</c>).
/// Public data list endpoints rely on form access and tenant scoping
/// </summary>
/// <param name="FormId">The form the token is for.</param>
/// <param name="TenantId">The form's tenant.</param>
/// <param name="ExpiresAtUtc">When the token expires.</param>
/// <param name="IsPublicGrant">
/// <c>true</c> when the token was issued because the form was public, so it holds only while the form stays public;
/// <c>false</c> when it was issued to a caller allowed to open the private form.
/// </param>
public sealed record FormAccessTokenClaims(long FormId, long TenantId, DateTime ExpiresAtUtc, bool IsPublicGrant = true);
