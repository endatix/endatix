using Endatix.Core.Exceptions;

namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// A job ran in a scope its work does not accept: a system job reached work written for a tenant, or a tenant job
/// reached work that belongs to no tenant. Retrying cannot change a job's scope, so the job runner fails the job at once.
/// </summary>
public sealed class JobScopeMismatchException(string detail)
    : InvalidOperationException(detail), IEndUserSafeError
{
    /// <summary>What the job's row records for a scope mismatch, wherever it is found.</summary>
    public const string WrongScope = "The job's scope does not match the work it runs.";

    /// <inheritdoc />
    public string EndUserMessage => WrongScope;
}
