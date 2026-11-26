using System.Globalization;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.CustomQuestions.Create;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>Keep the duplicate-name read and insert together on the repository's connection.</summary>
internal sealed class SerializeCustomQuestionCreation(AppDbContext db, ITransactionLock locks, ITenantContext tenant)
    : IPipelineBehavior<CreateCustomQuestionCommand, Result<CustomQuestion>>
{
    public Task<Result<CustomQuestion>> Handle(CreateCustomQuestionCommand request,
        RequestHandlerDelegate<Result<CustomQuestion>> next, CancellationToken cancellationToken) =>
        tenant.TenantId is > 0 and var id ? ExecuteAsync(CatalogKey(id), next, cancellationToken)
            : Task.FromResult<Result<CustomQuestion>>(Result.Forbidden());

    private async Task<Result<CustomQuestion>> ExecuteAsync(TransactionLockRequest key,
        RequestHandlerDelegate<Result<CustomQuestion>> next, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            try
            {
                await locks.AcquireAsync(db.Database, key, cancellationToken);
            }
            catch (TransactionLockTimeoutException)
            {
                return Busy();
            }
            return await next();
        }
        return await CreateUnderLockAsync(key, next, cancellationToken);
    }

    // Matching uses the database collation. Equivalent names must take the same lock on both providers.
    private static TransactionLockRequest CatalogKey(long id) =>
        new(TransactionLockScopes.CUSTOM_QUESTION_NAMES, id.ToString(CultureInfo.InvariantCulture)) { Timeout = TimeSpan.FromSeconds(5) };

    private Task<Result<CustomQuestion>> CreateUnderLockAsync(TransactionLockRequest key, RequestHandlerDelegate<Result<CustomQuestion>> create, CancellationToken ct)
    {
        var dispatched = false;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            // Retry connection and lock acquisition, never replay a handler or an ambiguous commit.
            if (dispatched)
            {
                return Result<CustomQuestion>.Unavailable("Could not confirm creation. Check the question list and try again.");
            }
            await using var transaction = await TryBeginAsync(key, token);
            if (transaction is null)
            {
                return Busy();
            }
            dispatched = true;
            return await CreateAndCommitAsync(create, transaction, token);
        }, ct);
    }

    private static async Task<Result<CustomQuestion>> CreateAndCommitAsync(RequestHandlerDelegate<Result<CustomQuestion>> create,
        IDbContextTransaction transaction, CancellationToken ct)
    {
        var result = await create();
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(ct);
        }
        return result;
    }

    private async Task<IDbContextTransaction?> TryBeginAsync(TransactionLockRequest key, CancellationToken ct)
    {
        try
        {
            return await locks.BeginLockedTransactionAsync(db.Database, key, ct);
        }
        catch (TransactionLockTimeoutException)
        {
            return null;
        }
    }

    private static Result<CustomQuestion> Busy() =>
        Result.Unavailable("Another custom question is being created. Try again.");
}
