using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Email.Dtos;

namespace Endatix.Core.UseCases.Email.ListTemplates;

/// <summary>
/// Handler for the ListEmailTemplatesQuery.
/// </summary>
/// <param name="repository">The repository.</param>
public class ListEmailTemplatesHandler(
    IRepository<EmailTemplate> repository
) : IQueryHandler<ListEmailTemplatesQuery, Result<IEnumerable<EmailTemplateSummaryDto>>>
{
    /// <summary>
    /// Handles the ListEmailTemplatesQuery.
    /// </summary>
    /// <param name="request">The ListEmailTemplatesQuery.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<Result<IEnumerable<EmailTemplateSummaryDto>>> Handle(ListEmailTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await repository.ListAsync(cancellationToken);

        var dtos = templates
        .Select(t => new EmailTemplateSummaryDto(t.Id, t.Name, t.Subject, t.FromAddress))
        .ToList()
        .AsEnumerable();

        return Result.Success(dtos);
    }
}
