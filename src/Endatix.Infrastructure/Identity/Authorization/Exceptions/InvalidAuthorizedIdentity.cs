namespace Endatix.Infrastructure.Identity.Authorization.Exceptions;

/// <summary>
/// Exception thrown when an invalid authorized identity is encountered and extraction of authorization data cannot be completed.
/// </summary>
public sealed class InvalidAuthorizedIdentityException : Exception
{
    public InvalidAuthorizedIdentityException(string message) : base(message)
    {
    }
}