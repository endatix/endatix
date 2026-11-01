using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>A form's search key, named by the column it copies.</summary>
/// <param name="TenantId">The caller's tenant.</param>
/// <param name="FormId">The form.</param>
/// <param name="Key">The column key.</param>
public sealed record SearchKeyTarget(long TenantId, long FormId, string Key);

/// <summary>
/// Makes a column of the form searchable on a person's choice, and rebuilds the form's search copies for it.
/// </summary>
public sealed record AddSearchKeyCommand(SearchKeyTarget Target) : ICommand<Result<SubmissionGridSearchKeys>>;

/// <summary>
/// Stops answer filters reading a column a person made searchable.
/// </summary>
public sealed record RemoveSearchKeyCommand(SearchKeyTarget Target) : ICommand<Result<SubmissionGridSearchKeys>>;
