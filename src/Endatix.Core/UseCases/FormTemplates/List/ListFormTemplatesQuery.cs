using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.List;

/// <summary>
/// Query for listing form templates with pagination.
/// </summary>
public record ListFormTemplatesQuery(int? Page, int? PageSize) : IQuery<Result<IEnumerable<FormTemplateDto>>>;
