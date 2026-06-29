using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.List;

/// <summary>
/// Query for listing forms with pagination, search, and filters.
/// </summary>
public record ListFormsQuery(
    int? Page,
    int? PageSize,
    string? Search = null,
    bool? IsEnabled = null,
    bool? IsPublic = null,
    IEnumerable<string>? FilterExpressions = null,
    long? FolderId = null) : IQuery<Result<Paged<FormDto>>>;
