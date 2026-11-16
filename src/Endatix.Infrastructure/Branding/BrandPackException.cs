namespace Endatix.Infrastructure.Branding;

/// <summary>
/// A mounted brand pack that cannot be used. Thrown at startup so the API does not run with a half-applied brand.
/// </summary>
public sealed class BrandPackException : Exception
{
    /// <summary>Creates the exception with a message that names the file and every failing field path.</summary>
    public BrandPackException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception, keeping the parse or IO failure as the inner exception.</summary>
    public BrandPackException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
