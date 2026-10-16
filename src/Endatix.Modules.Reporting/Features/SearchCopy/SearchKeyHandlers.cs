using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Adds a person's search key, then rebuilds the form's search copies from the stored answers before it answers.
/// </summary>
/// <remarks>
/// The rebuild runs in the request after the keys are committed, as a compile runs it. Rows it has not reached are
/// stamped with an older revision, so filters on the new key count them as not checked rather than as not matching,
/// and the columns catalog reports the key as pending. Adding the same key again finishes a rebuild that was cut off:
/// the rebuild skips rows already stamped with the current revision.
/// </remarks>
internal sealed class AddSearchKeyHandler(
    UserSearchKeys userSearchKeys,
    FormSearchCopy searchCopy,
    IFlattenedSubmissionRepository flattenedSubmissionRepository)
    : ICommandHandler<AddSearchKeyCommand, Result<SubmissionGridSearchKeys>>
{
    public async Task<Result<SubmissionGridSearchKeys>> Handle(AddSearchKeyCommand request, CancellationToken cancellationToken)
    {
        var target = request.Target;
        var added = await userSearchKeys.ChangeAsync(target, schema => searchCopy.AddUserKey(schema, target.Key), cancellationToken);
        if (!added.IsSuccess)
        {
            return added.ToErrorResult<SubmissionGridSearchKeys>();
        }

        await searchCopy.RebuildAsync(target.TenantId, target.FormId, cancellationToken);
        return await FormSearchKeyStatus.ReadGridSearchKeysAsync(added.Value, flattenedSubmissionRepository, cancellationToken);
    }
}

/// <summary>
/// Removes a person's search key. Filters stop reading it at once; its values stay in the rows until they are next
/// rebuilt or flattened, where nothing reads them.
/// </summary>
internal sealed class RemoveSearchKeyHandler(
    UserSearchKeys userSearchKeys,
    IFlattenedSubmissionRepository flattenedSubmissionRepository)
    : ICommandHandler<RemoveSearchKeyCommand, Result<SubmissionGridSearchKeys>>
{
    public async Task<Result<SubmissionGridSearchKeys>> Handle(RemoveSearchKeyCommand request, CancellationToken cancellationToken)
    {
        var target = request.Target;
        var removed = await userSearchKeys.ChangeAsync(
            target,
            schema => FormSearchKeys.Of(schema).RemoveUser(target.Key),
            cancellationToken);

        return removed.IsSuccess
            ? await FormSearchKeyStatus.ReadGridSearchKeysAsync(removed.Value, flattenedSubmissionRepository, cancellationToken)
            : removed.ToErrorResult<SubmissionGridSearchKeys>();
    }
}
