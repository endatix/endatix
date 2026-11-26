using System.Globalization;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.CustomQuestions.Create;
using MediatR;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>Keep the duplicate-name read and insert together on the repository's connection.</summary>
internal sealed class SerializeCustomQuestionCreation(AppDbContext db, ITransactionLock locks, ITenantContext tenant)
    : IPipelineBehavior<CreateCustomQuestionCommand, Result<CustomQuestion>>
{
    public async Task<Result<CustomQuestion>> Handle(CreateCustomQuestionCommand request,
        RequestHandlerDelegate<Result<CustomQuestion>> next, CancellationToken cancellationToken)
    {
        var id = tenant.TenantId;
        if (id <= 0)
        {
            return Result.Forbidden();
        }
        try
        {
            return await CreateUnderLockAsync(id, next, cancellationToken);
        }
        catch (TransactionLockTimeoutException)
        {
            return Result.Unavailable("Another custom question is being created. Try again.");
        }
    }

    private async Task<Result<CustomQuestion>> CreateUnderLockAsync(long id, RequestHandlerDelegate<Result<CustomQuestion>> create, CancellationToken ct)
    {
        // Matching uses the database collation. Equivalent names must take the same lock on both providers.
        var key = new TransactionLockRequest(TransactionLockScopes.CustomQuestionNames, id.ToString(CultureInfo.InvariantCulture))
        { Timeout = TimeSpan.FromSeconds(5) };
        await using var transaction = await locks.BeginLockedTransactionAsync(db.Database, key, ct);
        var result = await create();
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(ct);
        }
        return result;
    }
}
