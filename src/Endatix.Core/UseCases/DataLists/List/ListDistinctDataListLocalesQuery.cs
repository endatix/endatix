using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.DataLists.List;

/// <summary>
/// Returns distinct culture codes stored on tenant data lists (DefaultLocale ∪ AvailableLocales).
/// </summary>
public sealed record ListDistinctDataListLocalesQuery()
    : IQuery<Result<IReadOnlyList<string>>>;
