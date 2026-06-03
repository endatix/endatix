using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Email.Dtos;

namespace Endatix.Core.UseCases.Email.ListTemplates;

/// <summary>
/// Query to list email templates.
/// </summary>
/// <returns>The result.</returns>
public record ListEmailTemplatesQuery() : IQuery<Result<IEnumerable<EmailTemplateSummaryDto>>>;
