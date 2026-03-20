namespace Endatix.Infrastructure.Caching;

/// <summary>
/// Exception thrown when a cached factory returns an unsuccessful result.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="FailedResultException{T}"/> class.
/// </remarks>
/// <param name="result">The result that caused the exception.</param>
internal sealed class FailedResultException<T>(Endatix.Core.Infrastructure.Result.Result<T> result) : Exception("Cached factory returned unsuccessful result.")
{
    /// <summary>
    /// The result that caused the exception.
    /// </summary>
    public Endatix.Core.Infrastructure.Result.Result<T> Result { get; } = result;
}
