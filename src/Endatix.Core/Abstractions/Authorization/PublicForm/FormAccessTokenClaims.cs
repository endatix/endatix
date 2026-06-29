namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Claims carried by a minimal form-scoped ReBAC JWT (<c>frm</c> + <c>tid</c>).
/// Public data list endpoints rely on form access and tenant scoping
/// </summary>
public sealed record FormAccessTokenClaims(long FormId, long TenantId, DateTime ExpiresAtUtc);
