﻿namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// This Represents the request for the "/login" endpoint, handled by the <see cref="Login.HandleAsync"/> method.
/// </summary>
public record LoginRequest(string Email, string Password)
{
    /// <summary>
    /// The Email of the user. Must be a valid email address
    /// </summary>
    public string Email { get; init; } = Email;

    /// <summary>
    /// The Password of the account
    /// </summary>
    public string Password { get; init; } = Password;
}