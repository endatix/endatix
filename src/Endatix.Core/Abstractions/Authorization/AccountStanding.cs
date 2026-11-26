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

    private DateTime Now => _clock?.Now.UtcDateTime ?? DateTime.UtcNow;

    private Result? Refusal(AuthorizationData snapshot)
    {
        if (snapshot.RequiredActionsUnavailable || string.IsNullOrWhiteSpace(snapshot.UserId) || snapshot.UserId == AuthorizationData.ANONYMOUS_USER_ID)
        {
            return Result.Unavailable(UNAVAILABLE_MESSAGE);
        }

        // The gate and operation share this scoped decision for the lifetime of the request.
        if (snapshot.RequiredActions.Any(action => action.Severity == RequiredActionSeverity.Blocking))
        {
            return new BlockingRefusal(snapshot.RequiredActions);
        }
        _admittedUntil = snapshot.ExpiresAt == default ? DateTime.MaxValue : snapshot.ExpiresAt;
        return null;
    }

    /// <summary>Reads the hydrated account snapshot when the operation does not already hold it.</summary>
    public async Task<Result<T>?> RefusalAsync<T>(CancellationToken cancellationToken) =>
        (await RefusalAsync(cancellationToken))?.ToErrorResult<T>();

    /// <summary>Checks whether the account may continue with the operation.</summary>
    public async Task<Result?> RefusalAsync(CancellationToken cancellationToken)
    {
        if (_authorization is null || _admittedUntil > Now)
        {
            return null;
        }

        var snapshot = await _authorization.GetAuthorizationDataAsync(cancellationToken);
        if (IsExpired(snapshot))
        {
            // A deadline may pass between hydration and admission. Refresh once at the point of use.
            snapshot = await _authorization.GetAuthorizationDataAsync(cancellationToken);
            if (IsExpired(snapshot))
            {
                return Result.Unavailable(UNAVAILABLE_MESSAGE);
            }
        }
        return snapshot.IsSuccess ? Refusal(snapshot.Value) : Result.Unavailable(UNAVAILABLE_MESSAGE);
    }

    private bool IsExpired(Result<AuthorizationData> snapshot) =>
        snapshot.IsSuccess && snapshot.Value.ExpiresAt != default && snapshot.Value.ExpiresAt <= Now;

    private sealed class BlockingRefusal(IReadOnlyList<RequiredAction> actions)
        : Result(ResultStatus.Forbidden), IRequiredActionRefusal, IMappableError
    {
        public IReadOnlyList<RequiredAction> Actions => actions;
        public Result<T> MapError<T>() => new RequiredActionRefusal<T>(actions);
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
