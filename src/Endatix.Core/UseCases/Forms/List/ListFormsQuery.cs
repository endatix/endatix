using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Query for listing forms with pagination.
/// </summary>
/// <param name="Page">The page number for pagination</param>
/// <param name="PageSize">The number of items per page</param>
/// <param name="FilterExpressions">Optional filter expressions to narrow down the results</param>
/// <param name="FolderId">Optional folder filter</param>
public record ListFormsQuery(int? Page, int? PageSize, IEnumerable<string>? FilterExpressions = null, long? FolderId = null) : IQuery<Result<IEnumerable<FormDto>>>;
