using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>Checks the current account snapshot after ordinary access to an operation is established.</summary>
[Experimental("ENDATIX0006")]
public sealed class AccountStanding
{
    private readonly ICurrentUserAuthorizationService? _authorization;

    /// <summary>Creates the dormant decision used by shipped hosts until all account operations are covered.</summary>
    public AccountStanding() { }

    internal AccountStanding(ICurrentUserAuthorizationService authorization) => _authorization = authorization;

    /// <summary>Whether an operation needs its standing preflight. No shipped host enables it yet.</summary>
    public bool IsEnforced => _authorization is not null;

    /// <summary>Uses a snapshot the ordinary resource authorization already read.</summary>
    public Result<T>? Refusal<T>(AuthorizationData snapshot)
    {
        if (_authorization is null)
        {
            return null;
        }

        if (snapshot.RequiredActionsUnavailable)
        {
            return Result<T>.Unavailable("Required action status is unavailable. Try again.");
        }

        return snapshot.RequiredActions.Any(action => action.Severity == RequiredActionSeverity.Blocking)
            ? new RequiredActionRefusal<T>(snapshot.RequiredActions)
            : null;
    }

    /// <summary>Reads the hydrated account snapshot when the operation does not already hold it.</summary>
    public async Task<Result<T>?> RefusalAsync<T>(CancellationToken cancellationToken)
    {
        if (_authorization is null)
        {
            return null;
        }

        var snapshot = await _authorization.GetAuthorizationDataAsync(cancellationToken);
        return snapshot.IsSuccess ? Refusal<T>(snapshot.Value) : snapshot.ToErrorResult<T>();
    }

    /// <summary>Checks standing for an operation whose success has no payload.</summary>
    public async Task<Result?> RefusalAsync(CancellationToken cancellationToken)
    {
        var refusal = await RefusalAsync<Result>(cancellationToken);
        return refusal switch
        {
            null => null,
            RequiredActionRefusal<Result> blocked => new EmptyOperationRefusal(blocked),
            _ => new EmptyOperationError(refusal)
        };
    }

    private sealed class EmptyOperationError : Result
    {
        internal EmptyOperationError(Result<Result> error) : base(error.Status)
        {
            Errors = error.Errors;
            ValidationErrors = error.ValidationErrors;
            CorrelationId = error.CorrelationId;
            Existing = error.Existing;
        }
    }

    private sealed class EmptyOperationRefusal(RequiredActionRefusal<Result> refusal)
        : Result(refusal.Status), IRequiredActionRefusal, IMappableError
    {
        public IReadOnlyList<RequiredAction> Actions => refusal.Actions;
        public Result<T> MapError<T>() => refusal.MapError<T>();
    }
}

/// <summary>Distinguishes a standing refusal from an ordinary permission denial.</summary>
[Experimental("ENDATIX0006")]
public interface IRequiredActionRefusal
{
    /// <summary>The complete, caller-visible snapshot, in the same order as auth/me.</summary>
    IReadOnlyList<RequiredAction> Actions { get; }
}

internal sealed class RequiredActionRefusal<T>(IReadOnlyList<RequiredAction> actions)
    : Result<T>(ResultStatus.Forbidden), IRequiredActionRefusal, IMappableError
{
    public IReadOnlyList<RequiredAction> Actions { get; } = actions;

    public Result<TDestination> MapError<TDestination>() => new RequiredActionRefusal<TDestination>(Actions);
}
