using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Query for listing forms with pagination.
/// </summary>
/// <summary>
/// Query for listing forms with pagination.
/// </summary>
/// <param name="Page">The page number for pagination</param>
/// <param name="PageSize">The number of items per page</param>
/// <param name="FilterExpressions">Optional filter expressions to narrow down the results</param>
public record ListFormsQuery(int? Page, int? PageSize, IEnumerable<string>? FilterExpressions = null) : IQuery<Result<IEnumerable<FormDto>>>;
