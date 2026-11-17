using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.BackgroundJobs;

/// <summary>
/// The claimed job a handler is being asked to execute.
/// </summary>
/// <param name="JobId">Identifies the row; also the correlation id clients poll on.</param>
/// <param name="JobType">The router key this handler was resolved for.</param>
/// <param name="Scope">
/// Whose work the job is. For a tenant job, <b>every query the handler issues must be scoped to
/// <see cref="TenantId"/> explicitly</b> — see the handler contract on <see cref="IBackgroundJobHandler"/>.
/// A system job has no tenant and reads only app-level data.
/// </param>
/// <param name="PayloadJson">The input supplied at enqueue time.</param>
/// <param name="AttemptCount">
/// Which attempt this is, starting at 1. Useful for logging and for handlers that want to degrade on
/// a later attempt; retry policy itself is the runner's concern, not the handler's.
/// </param>
public sealed record BackgroundJobContext(
    long JobId,
    string JobType,
    JobScope Scope,
    string PayloadJson,
    int AttemptCount)
{
    /// <summary>The context of a job of tenant <paramref name="TenantId"/>.</summary>
    public BackgroundJobContext(long JobId, string JobType, long TenantId, string PayloadJson, int AttemptCount)
        : this(JobId, JobType, JobScope.ForTenant(TenantId), PayloadJson, AttemptCount)
    {
    }

    /// <summary>The owning tenant of a tenant job.</summary>
    /// <exception cref="InvalidOperationException">
    /// The job is a system job. A handler written for tenant work fails here rather than run with no tenant.
    /// </exception>
    public long TenantId => Scope.TenantId
        ?? throw new InvalidOperationException($"Job {JobId} ({JobType}) is a system job and has no tenant.");
}

/// <summary>
/// Executes one type of background job. Implementations are resolved from DI by
/// <see cref="JobType"/>, and may live in any assembly — including ones the jobs module does not
/// reference.
/// <para>
/// Handlers whose input is a typed record derive from <see cref="BackgroundJobHandler{TPayload}"/>, which
/// reads the input and returns a failure for input it cannot read. Implement this interface directly only
/// for infrastructure that handles input it does not know the type of.
/// </para>
/// <para>
/// Handlers never reference the scheduler that runs them. The obligations below are what the
/// executing host holds every handler to.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Returning versus throwing is the retry signal, and it is the only one.</b>
/// </para>
/// <list type="table">
///   <item>
///     <term>returns success</term>
///     <description>The job completes.</description>
///   </item>
///   <item>
///     <term>returns a failure <see cref="Result"/></term>
///     <description>
///     A <em>deterministic</em> failure — a missing schema, an invalid payload, a deleted endpoint.
///     The job goes terminal immediately and is never retried.
///     </description>
///   </item>
///   <item>
///     <term>throws</term>
///     <description>
///     A <em>presumed transient</em> failure — a DB timeout, a full disk, an HTTP 5xx. The job backs
///     off and retries, and dead-letters once its attempt budget is spent.
///     </description>
///   </item>
/// </list>
/// <para>
/// Getting this backwards is expensive in opposite directions: throwing on a deterministic error
/// re-runs work that can never succeed until the attempt budget is gone, while returning failure on
/// a transient one discards work that would have succeeded on a second try.
/// </para>
/// <para>
/// Three further obligations, each invisible until it hurts in production:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///     <b>Scope every query of a tenant job to <see cref="BackgroundJobContext.TenantId"/> explicitly.</b> The
///     ambient tenant filter resolves to "no tenant" outside a request, which makes it permissive
///     rather than restrictive — a handler that queries as if it were in a request reads every
///     tenant's data.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Honour the <see cref="CancellationToken"/>.</b> A handler that ignores it cannot be
///     cancelled by a user, and cannot be stopped when it exceeds its runtime ceiling.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Do not hold one <c>DbContext</c> for the length of the job.</b> Open a fresh DI scope per
///     chunk via <c>IServiceScopeFactory</c>. A change tracker held for minutes accumulates every
///     row streamed through it.
///     </description>
///   </item>
/// </list>
/// </remarks>
public interface IBackgroundJobHandler
{
    /// <summary>
    /// The <see cref="BackgroundJobRequest.JobType"/> this handler is registered for. Must be unique
    /// across all registered handlers, so that routing a job to a handler is unambiguous.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Executes the job. See the remarks on <see cref="IBackgroundJobHandler"/> for the
    /// return-versus-throw retry contract and the three handler obligations.
    /// </summary>
    Task<Result> ExecuteAsync(BackgroundJobContext job, CancellationToken cancellationToken);
}
