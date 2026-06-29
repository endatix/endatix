namespace Endatix.Core.Abstractions.Authorization.PublicForm;

/// <summary>
/// Issued form access token for runtime data list calls.
/// </summary>
public sealed record FormAccessTokenDto(string Token, DateTime ExpiresAtUtc);
