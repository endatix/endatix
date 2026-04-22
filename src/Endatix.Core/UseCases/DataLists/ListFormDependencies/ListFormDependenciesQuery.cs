using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.UseCases.DataLists.ListFormDependencies;

public sealed record ListFormDependenciesQuery(long DataListId)
    : IQuery<Result<IReadOnlyCollection<FormDto>>>;
