using System.Diagnostics.CodeAnalysis;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>Checks the current account snapshot after framework authorization.</summary>
[Experimental("ENDATIX0006")]
public sealed class AccountStanding
{
    private readonly ICurrentUserAuthorizationService? _authorization;
    private readonly IDateTimeProvider? _clock;
    private DateTime _admittedUntil;
    private const string UNAVAILABLE_MESSAGE = "Required action status is unavailable. Try again.";

    /// <summary>Creates the default decision, which does not enforce required actions.</summary>
    public AccountStanding() { }

    internal AccountStanding(ICurrentUserAuthorizationService authorization, IDateTimeProvider? clock = null)
    {
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>Whether an operation needs its standing preflight.</summary>
    public bool IsEnforced => _authorization is not null;

    private Result<T>? Refusal<T>(AuthorizationData snapshot)
    {
        if (snapshot.RequiredActionsUnavailable || string.IsNullOrWhiteSpace(snapshot.UserId) || snapshot.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result<T>.Unavailable(UNAVAILABLE_MESSAGE);
        }

        // The gate and operation share this scoped decision for the lifetime of the request.
        if (snapshot.RequiredActions.Any(action => action.Severity == RequiredActionSeverity.Blocking))
        {
            return new RequiredActionRefusal<T>(snapshot.RequiredActions);
        }
        _admittedUntil = snapshot.ExpiresAt == default ? DateTime.MaxValue : snapshot.ExpiresAt;
        return null;
    }

    /// <summary>Reads the hydrated account snapshot when the operation does not already hold it.</summary>
    public async Task<Result<T>?> RefusalAsync<T>(CancellationToken cancellationToken)
    {
        if (_authorization is null || _admittedUntil > (_clock?.Now.UtcDateTime ?? DateTime.UtcNow))
        {
            return null;
        }

        var snapshot = await _authorization.GetAuthorizationDataAsync(cancellationToken);
        return snapshot.IsSuccess ? Refusal<T>(snapshot.Value) : Result<T>.Unavailable(UNAVAILABLE_MESSAGE);
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
